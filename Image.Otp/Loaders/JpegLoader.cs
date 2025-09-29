using Image.Otp.Abstractions;
using Image.Otp.Core.Extensions;

namespace Image.Otp.Core.Loaders;

public sealed class JpegLoader : IImageLoader
{
    public bool CanLoad(ImgFormat format) => format == ImgFormat.Jpeg;

    public Image<T> Load<T>(string path) where T : unmanaged, IPixel<T>
    {
        using var fileStream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, FileOptions.SequentialScan);
        return Load<T>(fileStream);
    }

    public Image<T> Load<T>(byte[] data) where T : unmanaged, IPixel<T>
    {
        using var stream = new MemoryStream(data);
        return Load<T>(stream);
    }

    public Image<T> Load<T>(Stream stream) where T : unmanaged, IPixel<T> => stream.LoadJpeg<T>();
}
