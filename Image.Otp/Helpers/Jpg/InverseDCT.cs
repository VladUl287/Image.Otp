using System.Runtime.CompilerServices;

namespace Image.Otp.Core.Helpers.Jpg;

public static class InverseDCT
{
    private const int BLOCK_SIZE = 64;

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
    
    public static Span<double> Idct8x8InPlace(this Span<double> block)
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
}
