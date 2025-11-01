using Image.Otp.Core.Helpers.Jpg;
using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;

namespace Image.Otp.Core.Extensions;

public static class JpegDCTExtensions
{
    public static double[] Idct8x8InPlace(this double[] block)
    {
        ArgumentNullException.ThrowIfNull(block, nameof(block));
        Idct8x8InPlace(block.AsSpan());
        return block;
    }

    public static Span<double> Idct8x8InPlace(this Span<double> block)
    {
        if (Avx.IsSupported)
        {
            var result = block.ToArray().Select(c => float.Parse(c.ToString())).ToArray().AsSpan();
            AVXIDCTOPT.IDCT2D_SIMD_FOUR_ROWS(result);
        }

        JPEG_IDCT.IDCT2D_llm_In_Place(block);
        return block;
    }

    public static float[,] TwoDimensional(float[] values)
    {
        var oneDArray = new float[64];

        for (var i = 0; i < 64; i++)
        {
            oneDArray[i] = i;
        }

        var twoDArray = new float[8, 8];
        for (int row = 0; row < 8; row++)
        {
            for (int col = 0; col < 8; col++)
            {
                twoDArray[row, col] = oneDArray[row * 8 + col];
            }
        }
        return twoDArray;
    }

    public static double[] Idct8x8ScalarInPlace(this double[] block)
    {
        ArgumentNullException.ThrowIfNull(block, nameof(block));
        Idct8x8ScalarInPlace(block.AsSpan());
        return block;
    }

    public static Span<double> Idct8x8ScalarInPlace(this Span<double> block)
    {
        JPEG_IDCT.IDCT2D_Scalar_In_Place(block);
        return block;
    }
}
