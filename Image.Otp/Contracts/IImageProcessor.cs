using Image.Otp.Pixels;

namespace Image.Otp.Contracts;

public interface IImageProcessor<T> where T : unmanaged, IPixel<T>
{
    Image<T> Process(Stream stream);
    Image<T> Process(byte[] data);
    Image<T> Process(string path);
}
