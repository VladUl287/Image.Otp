namespace Image.Otp.Abstractions;

public interface IPixel<T> : IEquatable<T>
{ }

public unsafe interface IPixelBuilder<T> where T : unmanaged, IPixel<T>
{
    T FromYCbCr(byte y, byte cb, byte cr);
}