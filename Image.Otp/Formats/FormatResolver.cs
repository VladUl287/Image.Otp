using System.Runtime.CompilerServices;

namespace Image.Otp.Core.Formats;

public static class FormatResolver
{
    public static ImageFormat Resolve(Stream data)
    {
        const int MaxHeaderSize = 16;

        Span<byte> header = stackalloc byte[MaxHeaderSize];

        var originalPosition = data.Position;
        data.ReadExactly(header);
        data.Position = originalPosition;

        return Resolve(header);
    }

    public static ImageFormat Resolve(ReadOnlySpan<byte> header)
    {
        if (IsBmp(header))
            return ImageFormat.Bmp;

        if (IsJpeg(header))
            return ImageFormat.Jpeg;

        if (IsPng(header))
            return ImageFormat.Png;

        throw new NotSupportedException();
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

    //public static ImageFormat ResolveFormat(string name)
    //{
    //    if (string.IsNullOrEmpty(name))
    //        throw new ArgumentException("name is null or empty", nameof(name));

    //    var extension = Path.GetExtension(name)?.ToLowerInvariant();

    //    return extension switch
    //    {
    //        ".bmp" => ImageFormat.Bmp,
    //        ".jpg" or ".jpeg" or ".jfif" or ".jpe" or ".jfi" => ImageFormat.Jpeg,
    //        //".jp2" or ".j2k" or ".j2c" or ".jpm" or ".mjp2" => ImageFormat.Jpeg2000,
    //        ".png" => ImageFormat.Png,
    //        _ => throw new NotSupportedException($"Unsupported image extension: {extension}")
    //    };
    //}

}

public enum ImageFormat
{
    Bmp,
    Jpeg,
    Jpeg2000,
    Png
}
