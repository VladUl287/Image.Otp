using Image.Otp.Contracts;
using Image.Otp.Pixels;
using System.IO;

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
        throw new NotImplementedException();
    }
}
