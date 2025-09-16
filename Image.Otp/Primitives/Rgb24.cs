using Image.Otp.Pixels;
using System.Runtime.InteropServices;

namespace Image.Otp.Primitives;

[StructLayout(LayoutKind.Sequential)]
public readonly struct Rgb24(byte r, byte g, byte b) : IEquatable<Rgb24>, IPixel<Rgb24>
{
    public readonly byte R = r, G = g, B = b;

    public readonly bool Equals(Rgb24 other) => R == other.R && G == other.G && B == other.B;
}
