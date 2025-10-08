namespace Image.Otp.Core;

public static class JpegBlockProcessor
{
    public static readonly IReadOnlyList<int> ZigZag =
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

    private const int BLOCK_SIZE = 64;

    public static short[] ZigZagToNatural(short[] block)
    {
        var natural = new short[BLOCK_SIZE];
        for (var i = 0; i < BLOCK_SIZE; i++)
            natural[i] = block[ZigZag[i]];
        return natural;
    }

    public static ushort[] ZigZagToNatural(ushort[] block) => ZigZagToNatural(block);

    public static Span<double> ZigZagToNaturalInPlace(this Span<double> block)
    {
        if (block.Length != BLOCK_SIZE)
            throw new ArgumentException("Array must have exactly 64 elements");

        Span<double> temp = stackalloc double[BLOCK_SIZE];
        for (int i = 0; i < BLOCK_SIZE; i++)
            temp[i] = block[ZigZag[i]];

        temp.CopyTo(block);
        return block;
    }

    public static int ExtendSign(int value, int bitCount)
    {
        if (bitCount == 0) return 0;
        var vt = 1 << (bitCount - 1);
        return value < vt ? value - ((1 << bitCount) - 1) : value;
    }
}