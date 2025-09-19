using Image.Otp.Contracts;
using Image.Otp.Pixels;
using System.IO;

namespace Image.Otp;

public sealed class JpgProcessor<T> : IImageProcessor<T> where T : unmanaged, IPixel<T>
{
    public Task<ImageOtp<T>> ProcessAsync(Stream stream)
    {
        throw new NotImplementedException();
    }

    public Task<ImageOtp<T>> ProcessAsync(byte[] data)
    {
        throw new NotImplementedException();
    }

    public Task<ImageOtp<T>> ProcessAsync(string path)
    {
        throw new NotImplementedException();
    }
}
