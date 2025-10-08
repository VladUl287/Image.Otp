using System.Runtime.CompilerServices;

namespace Image.Otp.Core.Helpers.Jpg;

public static class JPEG_IDCT
{
    private const int BLOCK_SIZE = 64;

    /// <summary>
    /// IDCT inplace.
    /// Ported from https://github.com/norishigefukushima/dct_simd/blob/master/dct/dct8x8_simd.cpp#L239
    /// </summary>
    /// <param name="block"></param>
    public static void IDCT2D_llm_In_Place(Span<double> block)
    {
        Span<double> temp = stackalloc double[BLOCK_SIZE];

        for (var y = 0; y < 8; y++)
            IDCT1Dllm_64f(block, y * 8, temp, y * 8);

        Span<double> trans = stackalloc double[BLOCK_SIZE];
        Transpose8x8(temp, trans);

        for (var j = 0; j < 8; j++)
            IDCT1Dllm_64f(trans, j * 8, temp, j * 8);

        Transpose8x8(temp, block);

        for (var j = 0; j < BLOCK_SIZE; j++)
            block[j] *= 0.125;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void Transpose8x8(Span<double> src, Span<double> dst)
    {
        for (int i = 0; i < 8; i++)
            for (int j = 0; j < 8; j++)
                dst[j * 8 + i] = src[i * 8 + j];
    }

    public static void IDCT1Dllm_64f(Span<double> y, int yOffset, Span<double> x, int xOffset)
    {
        double a0, a1, a2, a3, b0, b1, b2, b3;
        double z0, z1, z2, z3, z4;

        Span<double> r =
        [
            1.414214,
            1.387040,
            1.306563,
            1.175876,
            1.000000,
            0.785695,
            0.541196,
            0.275899,
        ];

        z0 = y[yOffset + 1] + y[yOffset + 7];
        z1 = y[yOffset + 3] + y[yOffset + 5];
        z2 = y[yOffset + 3] + y[yOffset + 7];
        z3 = y[yOffset + 1] + y[yOffset + 5];
        z4 = (z0 + z1) * r[3];

        z0 = z0 * (-r[3] + r[7]);
        z1 = z1 * (-r[3] - r[1]);
        z2 = z2 * (-r[3] - r[5]) + z4;
        z3 = z3 * (-r[3] + r[5]) + z4;

        b3 = y[yOffset + 7] * (-r[1] + r[3] + r[5] - r[7]) + z0 + z2;
        b2 = y[yOffset + 5] * (r[1] + r[3] - r[5] + r[7]) + z1 + z3;
        b1 = y[yOffset + 3] * (r[1] + r[3] + r[5] - r[7]) + z1 + z2;
        b0 = y[yOffset + 1] * (r[1] + r[3] - r[5] - r[7]) + z0 + z3;

        z4 = (y[yOffset + 2] + y[yOffset + 6]) * r[6];
        z0 = y[yOffset + 0] + y[yOffset + 4];
        z1 = y[yOffset + 0] - y[yOffset + 4];
        z2 = z4 - y[yOffset + 6] * (r[2] + r[6]);
        z3 = z4 + y[yOffset + 2] * (r[2] - r[6]);
        a0 = z0 + z3;
        a3 = z0 - z3;
        a1 = z1 + z2;
        a2 = z1 - z2;

        x[xOffset + 0] = a0 + b0;
        x[xOffset + 7] = a0 - b0;
        x[xOffset + 1] = a1 + b1;
        x[xOffset + 6] = a1 - b1;
        x[xOffset + 2] = a2 + b2;
        x[xOffset + 5] = a2 - b2;
        x[xOffset + 3] = a3 + b3;
        x[xOffset + 4] = a3 - b3;
    }

    public static void IDCT2D_Scalar_In_Place(Span<double> block)
    {
        if (block.Length != BLOCK_SIZE)
            throw new ArgumentException("Block must have exactly 64 elements");

        Span<double> tmp = stackalloc double[BLOCK_SIZE];

        // 1D IDCT on rows
        for (int y = 0; y < 8; y++)
        {
            for (int x = 0; x < 8; x++)
            {
                double sum = 0.0;
                for (int u = 0; u < 8; u++)
                {
                    sum += C(u) * block[y * 8 + u] * Math.Cos((2 * x + 1) * u * Math.PI / 16.0);
                }
                tmp[y * 8 + x] = sum * 0.5;
            }
        }

        // 1D IDCT on columns
        for (int x = 0; x < 8; x++)
        {
            for (int y = 0; y < 8; y++)
            {
                double sum = 0.0;
                for (int v = 0; v < 8; v++)
                {
                    sum += C(v) * tmp[v * 8 + x] * Math.Cos(((2 * y + 1) * v * Math.PI) / 16.0);
                }
                block[y * 8 + x] = sum * 0.5;
            }
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static double C(int u) => u == 0 ? 1.0 / Math.Sqrt(2.0) : 1.0;
}
