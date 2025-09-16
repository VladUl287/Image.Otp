using Image.Otp.Pixels;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;

namespace Image.Otp.Primitives;

[StructLayout(LayoutKind.Sequential, Pack = 1)]
public readonly struct Rgba32(byte r, byte g, byte b, byte a = 255) : IEquatable<Rgba32>, IPixel<Rgba32>
{
    public readonly byte R = r, G = g, B = b, A = a;

    //public readonly uint ToUInt32() => BitConverter.ToUInt32([R, G, B, A], 0);
    //public static Rgba32 FromUInt32(uint packed) => MemoryMarshal.Read<Rgba32>(BitConverter.GetBytes(packed));

    public readonly bool Equals(Rgba32 other) => R == other.R && G == other.G && B == other.B && A == other.A;

    public readonly override bool Equals([NotNullWhen(true)] object? obj)
    {
        return (obj is Rgba32 rgba32 && Equals(rgba32)) || base.Equals(obj);
    }

    public override int GetHashCode() => base.GetHashCode();

    public static bool operator ==(Rgba32 left, Rgba32 right) => left.Equals(right);
    public static bool operator !=(Rgba32 left, Rgba32 right) => !(left == right);
}
