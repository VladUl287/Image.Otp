using System.Numerics;

namespace Image.Otp.Abstractions;

public interface IPixel<T> : IEquatable<T>
{ }

public unsafe interface IPixelProcessor<T> where T : unmanaged, IPixel<T>
{
    void ProcessPixel(byte* srcPtr, int srcPos, T* dstPtr, int dstPos, int bytesPerPixel);

    T FromYCbCr(byte y, byte cb, byte cr);

    void FromYCbCr(ReadOnlySpan<byte> y, ReadOnlySpan<byte> cb, ReadOnlySpan<byte> cr, Span<T> output) { }
}