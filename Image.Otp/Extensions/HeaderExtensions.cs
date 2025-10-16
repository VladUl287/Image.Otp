using System.Runtime.CompilerServices;

namespace Image.Otp.Core.Extensions;

public static class HeaderExtensions
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static bool IsBmp(this ReadOnlySpan<byte> header) => header.Length >= 2 && header[0] == 0x42 && header[1] == 0x4D;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static bool IsJpeg(this ReadOnlySpan<byte> header) => header.Length >= 2 && header[0] == 0xFF && header[1] == 0xD8;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static bool IsPng(this ReadOnlySpan<byte> header) =>
        header.Length >= 8 && header[0] == 0x89 && header[1] == 0x50 &&
        header[2] == 0x4E && header[3] == 0x47 && header[4] == 0x0D &&
        header[5] == 0x0A && header[6] == 0x1A && header[7] == 0x0A;
}
