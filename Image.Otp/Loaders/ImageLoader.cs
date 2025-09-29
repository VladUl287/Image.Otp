using Image.Otp.Abstractions;

namespace Image.Otp.Core.Loaders;

public sealed class ImageLoader(IFormatResolver formatResolver, IEnumerable<IImageLoader> loaders) : IImageLoader
{
    public bool CanLoad(ImgFormat format) => loaders.Any(c => c.CanLoad(format));

    public Image<T> Load<T>(string path) where T : unmanaged, IPixel<T>
    {
        using var fileStream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, FileOptions.SequentialScan);
        var format = formatResolver.Resolve(path);
        return Load<T>(fileStream, format);
    }

    public Image<T> Load<T>(byte[] data) where T : unmanaged, IPixel<T>
    {
        using var stream = new MemoryStream(data);
        return Load<T>(stream);
    }

    public Image<T> Load<T>(Stream stream) where T : unmanaged, IPixel<T>
    {
        var format = formatResolver.Resolve(stream);
        return Load<T>(stream, format);
    }

    private Image<T> Load<T>(Stream stream, ImgFormat format) where T : unmanaged, IPixel<T>
    {
        ArgumentNullException.ThrowIfNull(stream);

        var loader = loaders.FirstOrDefault(l => l.CanLoad(format));

        ArgumentNullException.ThrowIfNull(loader);

        return loader.Load<T>(stream);
    }
}
