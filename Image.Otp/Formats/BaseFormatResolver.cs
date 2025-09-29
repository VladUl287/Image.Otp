using Image.Otp.Abstractions;
using System.Runtime.CompilerServices;

namespace Image.Otp.Core.Formats;

public class BaseFormatResolver : IFormatResolver
{
    public ImgFormat Resolve(string path) => ResolveFormat(path);
    public ImgFormat Resolve(Stream stream) => ResolveFormat(stream);

    public static ImgFormat ResolveFormat(Stream data)
    {
        const int MaxHeaderSize = 16;

        Span<byte> header = stackalloc byte[MaxHeaderSize];

        var originalPosition = data.Position;
        data.ReadExactly(header);
        data.Position = originalPosition;

        return ResolveFormat(header);
    }

    public static ImgFormat ResolveFormat(ReadOnlySpan<byte> header)
    {
        if (IsBmp(header))
            return ImgFormat.Bmp;

        if (IsJpeg(header))
            return ImgFormat.Jpeg;

        if (IsPng(header))
            return ImgFormat.Png;

        throw new NotSupportedException();
    }

    public static ImgFormat ResolveFormat(string fileName)
    {
        ArgumentException.ThrowIfNullOrEmpty(fileName, nameof(fileName));
        return Resolve(fileName.AsSpan());
    }

    public static ImgFormat Resolve(ReadOnlySpan<char> fileName)
    {
        var extension = Path.GetExtension(fileName);
        Span<char> lower = stackalloc char[extension.Length];
        fileName.ToLowerInvariant(lower);

        return lower switch
        {
            ".bmp" => ImgFormat.Bmp,
            ".jpg" or ".jpeg" or ".jfif" or ".jpe" or ".jfi" => ImgFormat.Jpeg,
            ".png" => ImgFormat.Png,
            //".jp2" or ".j2k" or ".j2c" or ".jpm" or ".mjp2" => ImageFormat.Jpeg2000,
            _ => throw new NotSupportedException($"Unsupported image extension: {extension}")
        };
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool IsBmp(ReadOnlySpan<byte> header) => header.Length >= 2 && header[0] == 0x42 && header[1] == 0x4D;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool IsJpeg(ReadOnlySpan<byte> header) => header.Length >= 2 && header[0] == 0xFF && header[1] == 0xD8;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool IsPng(ReadOnlySpan<byte> header) =>
        header.Length >= 8 && header[0] == 0x89 && header[1] == 0x50 &&
        header[2] == 0x4E && header[3] == 0x47 && header[4] == 0x0D &&
        header[5] == 0x0A && header[6] == 0x1A && header[7] == 0x0A;
}