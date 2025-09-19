using Image.Otp.Pixels;

namespace Image.Otp.Contracts;

public interface IImageProcessor<T> where T : unmanaged, IPixel<T>
{
    Task<ImageOtp<T>> ProcessAsync(Stream stream);
    Task<ImageOtp<T>> ProcessAsync(byte[] data);
    Task<ImageOtp<T>> ProcessAsync(string path);
}
