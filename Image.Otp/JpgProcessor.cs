using Image.Otp.Contracts;
using Image.Otp.Pixels;

namespace Image.Otp;

public sealed class JpgProcessor<T> : IImageProcessor<T> where T : unmanaged, IPixel<T>
{
    public Task<ImageOtp<T>> ProcessAsync(Stream imageStream)
    {
        throw new NotImplementedException();
    }

    public Task<ImageOtp<T>> ProcessAsync(byte[] imageData)
    {
        throw new NotImplementedException();
    }

    public Task<ImageOtp<T>> ProcessAsync(string filePath)
    {
        throw new NotImplementedException();
    }
}
