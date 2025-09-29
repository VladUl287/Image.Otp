namespace Image.Otp.Abstractions.Contracts.Formats;

public interface IImageFormatDetector
{
    bool CanHandle(ReadOnlySpan<byte> header);
    string FormatName { get; }
}
