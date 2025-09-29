namespace Image.Otp.Abstractions;

public interface IFormatResolver
{
    ImgFormat Resolve(string path);
    ImgFormat Resolve(Stream stream);
}
