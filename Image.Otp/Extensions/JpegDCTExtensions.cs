using Image.Otp.Core.Helpers.Jpg;

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
        JPEG_IDCT.IDCT2D_llm_In_Place(block);
        return block;
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
