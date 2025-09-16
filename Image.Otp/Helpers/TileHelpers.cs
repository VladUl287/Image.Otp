using Image.Otp.Primitives;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;

namespace Image.Otp.Helpers;

public static class TileHelpers
{
    const int RelationProcessorCore = 0;
    const int RelationNumaNode = 1;
    const int RelationCache = 2;       // The one we need
    const int RelationProcessorPackage = 3;
    const int RelationGroup = 4;
    const int RelationAll = 0xFFFF;

    public unsafe static int GetOptimalTileSize()
    {
        // Get cache line size (typically 64 bytes on modern CPUs)
        int cacheLineSize = 64; // Fallback value

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            // Windows: Query CPU cache info via kernel32
            [DllImport("kernel32.dll")]
            static extern bool GetLogicalProcessorInformation(IntPtr buffer, ref uint length);

            uint length = 0;
            GetLogicalProcessorInformation(IntPtr.Zero, ref length);
            var buffer = Marshal.AllocHGlobal((int)length);

            if (GetLogicalProcessorInformation(buffer, ref length))
            {
                var ptr = buffer;
                while (ptr < buffer + length)
                {
                    var info = Marshal.PtrToStructure<SYSTEM_LOGICAL_PROCESSOR_INFORMATION>(ptr);
                    if (info.Relationship == RelationCache && info.Cache.Level == 1)
                    {
                        cacheLineSize = info.Cache.LineSize;
                        break;
                    }
                    ptr += Marshal.SizeOf<SYSTEM_LOGICAL_PROCESSOR_INFORMATION>();
                }
            }
            Marshal.FreeHGlobal(buffer);
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            // Linux: Read from /sys/devices/system/cpu/cpu0/cache/
            try
            {
                var lines = File.ReadAllLines("/sys/devices/system/cpu/cpu0/cache/index0/coherency_line_size");
                if (lines.Length > 0 && int.TryParse(lines[0], out int size))
                    cacheLineSize = size;
            }
            catch { /* Fallback */ }
        }

        // Calculate tile size (aim for L1 cache capacity)
        int l1CacheSize = 32 * 1024; // Typical L1 size
        int elementsPerCacheLine = cacheLineSize / sizeof(Rgba32); // 16 for Rgba32 (4 bytes per pixel)
        int tileSide = (int)Math.Sqrt(l1CacheSize / sizeof(Rgba32)); // ~90 for 32KB L1

        // Round down to nearest multiple of SIMD width
        int simdWidth = Vector256.IsHardwareAccelerated ? 8 : 4;
        return (tileSide / simdWidth) * simdWidth; // E.g., 88
    }

    // Required struct for Windows
    [StructLayout(LayoutKind.Sequential)]
    struct CACHE_DESCRIPTOR
    {
        public byte Level;
        public byte Associativity;
        public ushort LineSize;
        public uint Size;
        public int Type;
    }

    [StructLayout(LayoutKind.Explicit)]
    struct SYSTEM_LOGICAL_PROCESSOR_INFORMATION
    {
        [FieldOffset(0)] public UIntPtr ProcessorMask;
        [FieldOffset(8)] public int Relationship;
        [FieldOffset(12)] public CACHE_DESCRIPTOR Cache;
    }
}
