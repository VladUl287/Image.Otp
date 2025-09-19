using Image.Otp.Contracts;
using Image.Otp.Extensions;
using Image.Otp.Pixels;

namespace Image.Otp;

public sealed class JpgProcessor<T> : IImageProcessor<T> where T : unmanaged, IPixel<T>
{
    public ImageOtp<T> Process(Stream stream)
    {
        throw new NotImplementedException();
    }

    public ImageOtp<T> Process(byte[] data)
    {
        throw new NotImplementedException();
    }

    public ImageOtp<T> Process(string path)
    {
        using var fileStream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, FileOptions.SequentialScan);
        return fileStream.LoadJpeg<T>();
    }
}
