namespace Image.Otp;

public static class JpegDecoderHelpers
{
    // Standard zig-zag map (index in zigzag -> row-major index)
    public static readonly int[] ZigZag =
    {
         0,  1,  5,  6, 14, 15, 27, 28,
         2,  4,  7, 13, 16, 26, 29, 42,
         3,  8, 12, 17, 25, 30, 41, 43,
         9, 11, 18, 24, 31, 40, 44, 53,
        10, 19, 23, 32, 39, 45, 52, 54,
        20, 22, 33, 38, 46, 51, 55, 60,
        21, 34, 37, 47, 50, 56, 59, 61,
        35, 36, 48, 49, 57, 58, 62, 63
    };

    // Convert zigzag-ordered coefficients to row-major 8x8
    public static short[] ZigZagToBlock(short[] zz)
    {
        var blk = new short[64];
        for (int i = 0; i < 64; i++)
        {
            int pos = ZigZag[i];
            blk[pos] = zz[i];
        }
        return blk;
    }

    public static short[] NaturalToZigzag(short[] natural)
    {
        var zz = new short[64];
        for (var i = 0; i < 64; i++)
        {
            // ZigZag[pos] = naturalIndex for zigzag position pos
            zz[i] = natural[ZigZag[i]];
        }
        return zz;
    }

    public static ushort[] NaturalToZigzag(ushort[] natural)
    {
        var zz = new ushort[64];
        for (var i = 0; i < 64; i++)
        {
            // ZigZag[pos] = naturalIndex for zigzag position pos
            zz[i] = natural[ZigZag[i]];
        }
        return zz;
    }

    public static short[] ZigZagToNatural(short[] zz)
    {
        var natural = new short[64];
        for (int i = 0; i < 64; i++)
        {
            natural[ZigZag[i]] = zz[i];
        }
        return natural;
    }

    // Extend value for JPEG variable length signed numbers
    public static int ExtendSign(int value, int bitCount)
    {
        if (bitCount == 0) return 0;
        int vt = 1 << (bitCount - 1);
        if (value < vt) return value - ((1 << bitCount) - 1);
        return value;
    }
}