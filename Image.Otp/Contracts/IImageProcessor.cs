using Image.Otp.Pixels;

namespace Image.Otp.Contracts;

public interface IImageProcessor<T> where T : unmanaged, IPixel<T>
{
    ImageOtp<T> Process(Stream stream);
    ImageOtp<T> Process(byte[] data);
    ImageOtp<T> Process(string path);
}
