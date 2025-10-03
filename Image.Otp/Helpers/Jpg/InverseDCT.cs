using System.Runtime.CompilerServices;

namespace Image.Otp.Core.Helpers.Jpg;

public static class InverseDCT
{
    private const int BLOCK_SIZE = 64;

    public static double[] Idct8x8InPlaceOpt(this double[] block)
    {
        Idct8x8InPlaceOpt(block.AsSpan());
        return block;
    }

    public static Span<double> Idct8x8InPlaceOpt(this Span<double> block)
    {
        if (block.Length != BLOCK_SIZE)
            throw new ArgumentException("Block must have exactly 64 elements");

        var coeff = 1.0 / Math.Sqrt(2.0);
        for (int i = 0; i < BLOCK_SIZE; i++)
            if (i % 8 == 0) block[i] *= coeff;

        // Temporary workspace (row pass then column pass)
        Span<double> tmp = stackalloc double[BLOCK_SIZE];

        // Row-wise 1D IDCT: process each of 8 rows
        for (int r = 0; r < 8; r++)
            Idct1D(block, r * 8, tmp, r * 8);

        // Transpose while copying to prepare column-wise processing
        Span<double> trans = stackalloc double[BLOCK_SIZE];
        for (int r = 0; r < 8; r++)
            for (int c = 0; c < 8; c++)
                trans[c * 8 + r] = tmp[r * 8 + c];

        for (int i = 0; i < BLOCK_SIZE; i++)
            if (i % 8 == 0) trans[i] *= coeff; // DC component scaling

        // Column-wise 1D IDCT (on transposed rows = original columns)
        for (int r = 0; r < 8; r++)
            Idct1D(trans, r * 8, tmp, r * 8);

        // Transpose back to normal order and apply final scaling (1/4)
        for (int r = 0; r < 8; r++)
            for (int c = 0; c < 8; c++)
                block[r * 8 + c] = tmp[c * 8 + r] * 0.25; // scale factor for IDCT

        return block;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void Idct1D(Span<double> src, int srcBase, Span<double> dst, int dstBase)
    {
        const double c1 = 0.9807852804032304; // cos(pi/16)
        const double c2 = 0.9238795325112867; // cos(2pi/16)
        const double c3 = 0.8314696123025452; // cos(3pi/16)
        const double c4 = 0.7071067811865476; // cos(4pi/16) = 1/sqrt(2)
        const double c5 = 0.5555702330196023; // cos(5pi/16)
        const double c6 = 0.3826834323650898; // cos(6pi/16)
        const double c7 = 0.19509032201612825;// cos(7pi/16)

        // Using an AAN/Loeffler style staged algorithm
        double x0 = src[srcBase + 0];
        double x1 = src[srcBase + 1];
        double x2 = src[srcBase + 2];
        double x3 = src[srcBase + 3];
        double x4 = src[srcBase + 4];
        double x5 = src[srcBase + 5];
        double x6 = src[srcBase + 6];
        double x7 = src[srcBase + 7];

        // Even part
        double a0 = x0 + x4;
        double a1 = x0 - x4;
        double a2 = x2 * c2 - x6 * c6;
        double a3 = x2 * c6 + x6 * c2;

        double b0 = a0 + a3;
        double b1 = a1 + a2;
        double b2 = a1 - a2;
        double b3 = a0 - a3;

        // Odd part
        double d0 = x1 * c1 - x7 * c7;
        double d1 = x1 * c7 + x7 * c1;
        double d2 = x5 * c5 - x3 * c3;
        double d3 = x5 * c3 + x3 * c5;

        double e0 = d0 + d3;
        double e1 = d1 + d2;
        double e2 = d1 - d2;
        double e3 = d0 - d3;

        // Combine
        dst[dstBase + 0] = b0 + e1;
        dst[dstBase + 1] = b1 + e0;
        dst[dstBase + 2] = b2 + e3;
        dst[dstBase + 3] = b3 + e2;
        dst[dstBase + 4] = b3 - e2;
        dst[dstBase + 5] = b2 - e3;
        dst[dstBase + 6] = b1 - e0;
        dst[dstBase + 7] = b0 - e1;
    }

    public static double[] Idct8x8InPlace(this double[] block)
    {
        ArgumentNullException.ThrowIfNull(block, nameof(block));

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
                    sum += Coefficient(u) * block[y * 8 + u] * Math.Cos((2 * x + 1) * u * Math.PI / 16.0);
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
                    sum += Coefficient(v) * tmp[v * 8 + x] * Math.Cos((2 * y + 1) * v * Math.PI / 16.0);
                }
                block[y * 8 + x] = sum * 0.5;
            }
        }

        return block;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static double Coefficient(int u) => u == 0 ? 1.0 / Math.Sqrt(2.0) : 1.0;

    private static readonly double[] CosTable = [
      Math.Cos(0 * Math.PI / 16.0), Math.Cos(1 * Math.PI / 16.0),
        Math.Cos(2 * Math.PI / 16.0), Math.Cos(3 * Math.PI / 16.0),
        Math.Cos(4 * Math.PI / 16.0), Math.Cos(5 * Math.PI / 16.0),
        Math.Cos(6 * Math.PI / 16.0), Math.Cos(7 * Math.PI / 16.0),
        // Additional terms for (2x+1) factors
        Math.Cos(1 * Math.PI / 16.0), Math.Cos(3 * Math.PI / 16.0),
        Math.Cos(5 * Math.PI / 16.0), Math.Cos(7 * Math.PI / 16.0),
        Math.Cos(9 * Math.PI / 16.0), Math.Cos(11 * Math.PI / 16.0),
        Math.Cos(13 * Math.PI / 16.0), Math.Cos(15 * Math.PI / 16.0)
  ];

    private static readonly double[] Coefficients = [1.0 / Math.Sqrt(2.0), 1.0, 1.0, 1.0, 1.0, 1.0, 1.0, 1.0];

    public static double[] Idct8x8Optimized(this double[] block)
    {
        ArgumentNullException.ThrowIfNull(block);
        if (block.Length != BLOCK_SIZE)
            throw new ArgumentException("Block must have exactly 64 elements");

        Span<double> tmp = stackalloc double[BLOCK_SIZE];

        for (int y = 0; y < 8; y++)
        {
            Idct1D8(block.AsSpan(y * 8, 8), tmp.Slice(y * 8, 8), Coefficients, CosTable);
        }

        for (int x = 0; x < 8; x++)
        {
            Span<double> column = stackalloc double[8];
            for (int v = 0; v < 8; v++)
                column[v] = tmp[v * 8 + x];

            Span<double> result = stackalloc double[8];
            Idct1D8(column, result, Coefficients, CosTable);

            for (int y = 0; y < 8; y++)
                block[y * 8 + x] = result[y] * 0.5;
        }

        return block;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void Idct1D8(ReadOnlySpan<double> input, Span<double> output, double[] coefficients, double[] cosTable)
    {
        for (int x = 0; x < 8; x++)
        {
            double sum = 0.0;
            for (int u = 0; u < 8; u++)
            {
                var cosIndex = (2 * x + 1) * u % 16; // Use modulo for table lookup
                sum += coefficients[u] * input[u] * cosTable[cosIndex];
            }
            output[x] = sum * 0.5;
        }
    }
}
