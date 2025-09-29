namespace Image.Otp.Abstractions;

public interface IImgLoader
{
    Image<T> Load<T>(string path) where T : unmanaged, IPixel<T>;
    Image<T> Load<T>(Stream stream) where T : unmanaged, IPixel<T>;
    Image<T> Load<T>(Stream stream, ImgFormat format) where T : unmanaged, IPixel<T>;
}
