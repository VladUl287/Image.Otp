using Image.Otp.Core.Pixels;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;

namespace Image.Otp.Core.Primitives;

[StructLayout(LayoutKind.Sequential, Pack = 1)]
public readonly struct Rgb24(byte r, byte g, byte b) : IPixel<Rgb24>, IEquatable<Rgb24>
{
    public readonly byte R = r, G = g, B = b;

    public readonly bool Equals(Rgb24 other) => R == other.R && G == other.G && B == other.B;

    public readonly override bool Equals([NotNullWhen(true)] object? obj)
    {
        return (obj is Rgb24 rgb24 && Equals(rgb24)) || base.Equals(obj);
    }

    public override int GetHashCode() => base.GetHashCode();

    public static bool operator ==(Rgb24 left, Rgb24 right) => left.Equals(right);
    public static bool operator !=(Rgb24 left, Rgb24 right) => !(left == right);
}
