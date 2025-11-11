namespace Image.Otp.Core.Extensions;

public static class ZigZagExtensions
{
    public static ReadOnlySpan<int> ZigZag =>
    [
         0,  1,  5,  6, 14, 15, 27, 28,
         2,  4,  7, 13, 16, 26, 29, 42,
         3,  8, 12, 17, 25, 30, 41, 43,
         9, 11, 18, 24, 31, 40, 44, 53,
        10, 19, 23, 32, 39, 45, 52, 54,
        20, 22, 33, 38, 46, 51, 55, 60,
        21, 34, 37, 47, 50, 56, 59, 61,
        35, 36, 48, 49, 57, 58, 62, 63
    ];

    public static ReadOnlySpan<int> InverseZigZag =>
    [
         0,  1,  8, 16,  9,  2,  3, 10,
        17, 24, 32, 25, 18, 11,  4,  5,
        12, 19, 26, 33, 40, 48, 41, 34,
        27, 20, 13,  6,  7, 14, 21, 28,
        35, 42, 49, 56, 57, 50, 43, 36,
        29, 22, 15, 23, 30, 37, 44, 51,
        58, 59, 52, 45, 38, 31, 39, 46,
        53, 60, 61, 54, 47, 55, 62, 63
    ];

    private const int BLOCK_SIZE = 64;

    public static T[] UnZigZag<T>(this T[] block) where T : unmanaged
    {
        block.AsSpan().UnZigZag();
        return block;
    }

    public unsafe static Span<T> UnZigZag<T>(this Span<T> block) where T : unmanaged
    {
        if (block.Length != BLOCK_SIZE)
            throw new ArgumentException("Array must have exactly 64 elements");

        Span<T> temp = stackalloc T[BLOCK_SIZE];
        for (var i = 0; i < BLOCK_SIZE; i++)
            temp[i] = block[ZigZag[i]];

        temp.CopyTo(block);
        return block;
    }

    public static int ExtendSign(int value, int bitCount)
    {
        if (bitCount == 0) return 0;
        var vt = 1 << bitCount - 1;
        return value < vt ? value - ((1 << bitCount) - 1) : value;
    }
}