namespace Image.Otp;

public static class JpegDecoderHelpers
{
    public static readonly int[] ZigZag =
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

    public static short[] NaturalToZigzag(short[] natural)
    {
        var zz = new short[64];
        for (var i = 0; i < 64; i++)
        {
            zz[i] = natural[ZigZag[i]];
        }
        return zz;
    }

    public static double[] NaturalToZigzag(double[] natural)
    {
        var zz = new double[64];
        for (var i = 0; i < 64; i++)
        {
            zz[i] = natural[ZigZag[i]];
        }
        return zz;
    }

    private const int BLOCK_SIZE = 64;

    public static double[] ZigzagInPlace(this double[] block)
    {
        if (block.Length != BLOCK_SIZE)
            throw new ArgumentException("Array must have exactly 64 elements");

        Span<double> temp = stackalloc double[BLOCK_SIZE];
        for (int i = 0; i < BLOCK_SIZE; i++)
        {
            temp[i] = block[ZigZag[i]];
        }
        temp.CopyTo(block);

        return block;
    }

    public static Span<double> ZigZagInPlace(this Span<double> block)
    {
        if (block.Length != BLOCK_SIZE)
            throw new ArgumentException("Array must have exactly 64 elements");

        Span<double> temp = stackalloc double[BLOCK_SIZE];
        for (int i = 0; i < BLOCK_SIZE; i++)
        {
            temp[i] = block[ZigZag[i]];
        }
        temp.CopyTo(block);

        return block;
    }

    public static ushort[] NaturalToZigzag(ushort[] natural)
    {
        var zz = new ushort[64];
        for (var i = 0; i < 64; i++)
        {
            zz[i] = natural[ZigZag[i]];
        }
        return zz;
    }

    public static int ExtendSign(int value, int bitCount)
    {
        if (bitCount == 0) return 0;
        int vt = 1 << (bitCount - 1);
        if (value < vt) return value - ((1 << bitCount) - 1);
        return value;
    }
}