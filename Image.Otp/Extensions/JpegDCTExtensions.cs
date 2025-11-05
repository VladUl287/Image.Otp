using Image.Otp.Core.Helpers.Jpg;
using System.Runtime.Intrinsics.X86;

namespace Image.Otp.Core.Extensions;

public static class JpegDCTExtensions
{
    public static double[] IDCT8x8InPlace(this double[] block)
    {
        ArgumentNullException.ThrowIfNull(block, nameof(block));
        IDCT8x8InPlace(block.AsSpan());
        return block;
    }

    public static Span<double> IDCT8x8InPlace(this Span<double> block)
    {
        IDCT.IDCT2D_LLM(block);
        return block;
    }

    public static Span<float> IDCT8x8InPlace(this Span<float> block)
    {
        if (Avx.IsSupported)
            IDCT_AVX.IDCT2D_AVX(block);
        else
            IDCT.IDCT2D_LLM(block);
        return block;
    }
}
