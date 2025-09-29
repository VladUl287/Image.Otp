using Image.Otp.Abstractions;
using Image.Otp.Core.Extensions;

namespace Image.Otp.Core.Formats;

public class BaseFormatResolver : IFormatResolver
{
    public ImgFormat Resolve(string path) => ResolveByPath(path);
    public ImgFormat Resolve(Stream stream) => ResolveByStream(stream);

    public static ImgFormat ResolveByStream(Stream data)
    {
        const int MaxHeaderSize = 16;

        Span<byte> header = stackalloc byte[MaxHeaderSize];

        var originalPosition = data.Position;
        data.ReadExactly(header);
        data.Position = originalPosition;

        return ResolveByHeader(header);
    }

    public static ImgFormat ResolveByHeader(ReadOnlySpan<byte> header)
    {
        if (header.IsBmp())
            return ImgFormat.Bmp;

        if (header.IsJpeg())
            return ImgFormat.Jpeg;

        if (header.IsPng())
            return ImgFormat.Png;

        throw new NotSupportedException();
    }

    public static ImgFormat ResolveByPath(string path)
    {
        ArgumentException.ThrowIfNullOrEmpty(path, nameof(path));
        var extension = Path.GetExtension(path.AsSpan());
        return ResolveByExtension(extension);
    }

    public static ImgFormat ResolveByExtension(ReadOnlySpan<char> extension)
    {
        if (extension.IsEmpty)
            throw new NotSupportedException("File has no extension");

        Span<char> lowerExtension = stackalloc char[extension.Length];
        extension.ToLowerInvariant(lowerExtension);

        return lowerExtension switch
        {
            ".bmp" => ImgFormat.Bmp,
            ".jpg" or ".jpeg" or ".jfif" or ".jpe" or ".jfi" => ImgFormat.Jpeg,
            ".png" => ImgFormat.Png,
            //".jp2" or ".j2k" or ".j2c" or ".jpm" or ".mjp2" => ImageFormat.Jpeg2000,
            _ => throw new NotSupportedException($"Unsupported image extension: {extension}")
        };
    }
}