using Image.Otp.Abstractions;

namespace Image.Otp.Core.Loaders;

public sealed class BmpLoader : IImageLoader
{
    public bool CanLoad(ImgFormat format)
    {
        throw new NotImplementedException();
    }

    public Image<T> Load<T>(string path) where T : unmanaged, IPixel<T>
    {
        throw new NotImplementedException();
    }

    public Image<T> Load<T>(byte[] data) where T : unmanaged, IPixel<T>
    {
        throw new NotImplementedException();
    }

    public Image<T> Load<T>(Stream stream) where T : unmanaged, IPixel<T>
    {
        throw new NotImplementedException();
    }
}
