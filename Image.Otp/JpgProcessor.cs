using Image.Otp.Core.Contracts;
using Image.Otp.Core.Extensions;
using Image.Otp.Core.Pixels;

namespace Image.Otp;

public sealed class JpgProcessor<T> : IImageProcessor<T> where T : unmanaged, IPixel<T>
{
    public Image<T> Process(Stream stream)
    {
        throw new NotImplementedException();
    }

    public Image<T> Process(byte[] data)
    {
        throw new NotImplementedException();
    }

    public Image<T> Process(string path)
    {
        using var fileStream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, FileOptions.SequentialScan);
        return fileStream.LoadJpeg<T>();
    }
}
