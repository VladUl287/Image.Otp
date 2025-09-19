namespace Image.Otp.Contracts;

public interface IImageProcessor<T> where T : unmanaged
{
    Task<ImageOtp<T>> ProcessAsync(Stream imageStream);
    Task<ImageOtp<T>> ProcessAsync(byte[] imageData);
    Task<ImageOtp<T>> ProcessAsync(string filePath);
}
