using System;
using System.Runtime.InteropServices;
using System.IO;

namespace Image.Otp.Helpers;

public static class CacheInfo
{
    // Windows API declarations
    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool GetLogicalProcessorInformation(IntPtr buffer, ref uint returnedLength);

    private const int ERROR_INSUFFICIENT_BUFFER = 122;

    public static int GetCacheLineSize()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return GetWindowsCacheLineSize();
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            return GetLinuxCacheLineSize();
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            return GetMacCacheLineSize();
        }

        return 64; // Default fallback
    }

    public static int GetL1CacheSize()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return GetWindowsL1CacheSize();
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            return GetLinuxL1CacheSize();
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            return GetMacL1CacheSize();
        }

        return 32 * 1024; // 32KB default fallback
    }

    #region Windows Implementation
    [StructLayout(LayoutKind.Sequential)]
    private struct CACHE_DESCRIPTOR
    {
        public byte Level;
        public byte Associativity;
        public ushort LineSize;
        public uint Size;
        public int Type;
    }

    [StructLayout(LayoutKind.Explicit)]
    private struct SYSTEM_LOGICAL_PROCESSOR_INFORMATION
    {
        [FieldOffset(0)] public UIntPtr ProcessorMask;
        [FieldOffset(8)] public int Relationship;
        [FieldOffset(12)] public CACHE_DESCRIPTOR Cache;
    }

    private static int GetWindowsCacheLineSize()
    {
        uint bufferSize = 0;
        GetLogicalProcessorInformation(IntPtr.Zero, ref bufferSize);

        IntPtr buffer = Marshal.AllocHGlobal((int)bufferSize);
        try
        {
            if (GetLogicalProcessorInformation(buffer, ref bufferSize))
            {
                int elementSize = Marshal.SizeOf<SYSTEM_LOGICAL_PROCESSOR_INFORMATION>();
                for (int i = 0; i < bufferSize; i += elementSize)
                {
                    var info = Marshal.PtrToStructure<SYSTEM_LOGICAL_PROCESSOR_INFORMATION>(buffer + i);
                    if (info.Relationship == 2 /* RelationCache */ && info.Cache.Level == 1)
                    {
                        return info.Cache.LineSize;
                    }
                }
            }
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }

        return 64;
    }

    private static int GetWindowsL1CacheSize()
    {
        uint bufferSize = 0;
        GetLogicalProcessorInformation(IntPtr.Zero, ref bufferSize);

        IntPtr buffer = Marshal.AllocHGlobal((int)bufferSize);
        try
        {
            if (GetLogicalProcessorInformation(buffer, ref bufferSize))
            {
                int elementSize = Marshal.SizeOf<SYSTEM_LOGICAL_PROCESSOR_INFORMATION>();
                for (int i = 0; i < bufferSize; i += elementSize)
                {
                    var info = Marshal.PtrToStructure<SYSTEM_LOGICAL_PROCESSOR_INFORMATION>(buffer + i);
                    if (info.Relationship == 2 /* RelationCache */ && info.Cache.Level == 1)
                    {
                        return (int)info.Cache.Size;
                    }
                }
            }
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }

        return 32 * 1024;
    }
    #endregion

    #region Linux Implementation
    private static int GetLinuxCacheLineSize()
    {
        try
        {
            // Check for coherency line size (most reliable)
            string path = FindFirstCacheDir("coherency_line_size");
            if (path != null)
            {
                string lineSize = File.ReadAllText(path).Trim();
                if (int.TryParse(lineSize, out int size))
                {
                    return size;
                }
            }
        }
        catch { }

        return 64;
    }

    private static int GetLinuxL1CacheSize()
    {
        try
        {
            // Check for L1 data cache size
            string path = FindFirstCacheDir("size", level: 1);
            if (path != null)
            {
                string sizeText = File.ReadAllText(path).Trim();
                if (sizeText.EndsWith("K") &&
                    int.TryParse(sizeText.Substring(0, sizeText.Length - 1), out int sizeKb))
                {
                    return sizeKb * 1024;
                }
            }
        }
        catch { }

        return 32 * 1024;
    }

    private static string FindFirstCacheDir(string file, int level = -1)
    {
        const string cacheDir = "/sys/devices/system/cpu/cpu0/cache/";
        if (!Directory.Exists(cacheDir))
            return null;

        foreach (string dir in Directory.GetDirectories(cacheDir, "index*"))
        {
            try
            {
                // Check level if specified
                if (level > 0)
                {
                    string levelPath = Path.Combine(dir, "level");
                    if (File.Exists(levelPath))
                    {
                        string levelText = File.ReadAllText(levelPath).Trim();
                        if (!int.TryParse(levelText, out int dirLevel) || dirLevel != level)
                            continue;
                    }
                }

                // Check if target file exists
                string targetFile = Path.Combine(dir, file);
                if (File.Exists(targetFile))
                {
                    return targetFile;
                }
            }
            catch { }
        }

        return null;
    }
    #endregion

    #region macOS Implementation
    private static int GetMacCacheLineSize()
    {
        try
        {
            // Use sysctl on macOS
            var startInfo = new System.Diagnostics.ProcessStartInfo
            {
                FileName = "/usr/sbin/sysctl",
                Arguments = "-n hw.cachelinesize",
                RedirectStandardOutput = true,
                UseShellExecute = false
            };

            using var process = System.Diagnostics.Process.Start(startInfo);
            string output = process.StandardOutput.ReadToEnd().Trim();
            if (int.TryParse(output, out int size))
            {
                return size;
            }
        }
        catch { }

        return 64;
    }

    private static int GetMacL1CacheSize()
    {
        try
        {
            // Use sysctl on macOS
            var startInfo = new System.Diagnostics.ProcessStartInfo
            {
                FileName = "/usr/sbin/sysctl",
                Arguments = "-n hw.l1dcachesize",
                RedirectStandardOutput = true,
                UseShellExecute = false
            };

            using var process = System.Diagnostics.Process.Start(startInfo);
            string output = process.StandardOutput.ReadToEnd().Trim();
            if (int.TryParse(output, out int size))
            {
                return size;
            }
        }
        catch { }

        return 32 * 1024;
    }
    #endregion
}