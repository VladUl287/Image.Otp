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
            IDCT8x8_AVX2(block.ToArray().Select(c => float.Parse(c.ToString())).ToArray().AsSpan());
            return block;
        }

        JPEG_IDCT.IDCT2D_llm_In_Place(block);
        return block;
    }

    public unsafe static void IDCT8x8_AVX2(Span<float> block)
    {
        fixed (float* ptr = block)
        {
            AVXIDCT.TransformBlockAVX(ptr);
        }
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
