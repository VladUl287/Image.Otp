namespace Image.Otp.Abstractions;

public interface IImageLoader
{
    bool CanLoad(ImgFormat format);
    Image<T> Load<T>(string path) where T : unmanaged, IPixel<T>;
    Image<T> Load<T>(byte[] data) where T : unmanaged, IPixel<T>;
    Image<T> Load<T>(Stream stream) where T : unmanaged, IPixel<T>;
}
