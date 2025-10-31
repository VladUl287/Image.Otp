using System.Runtime.InteropServices;
using System.Runtime.Intrinsics.X86;
using System.Runtime.Intrinsics;
using System.Numerics;
using System.Runtime.CompilerServices;

namespace Image.Otp.Core.Extensions;

public unsafe class AVXIDCT
{
    private static readonly Vector256<float>[] CosConstants = new Vector256<float>[8];
    private static readonly Vector256<float>[] ScaleConstants = new Vector256<float>[8];

    static AVXIDCT()
    {
        for (int i = 0; i < 8; i++)
        {
            var cosValues = new float[8];

            for (int j = 0; j < 8; j++)
                cosValues[j] = (float)Math.Cos((2 * i + 1) * j * Math.PI / 16.0);

            CosConstants[i] = Vector256.Create(
                cosValues[0], cosValues[1], cosValues[2], cosValues[3],
                cosValues[4], cosValues[5], cosValues[6], cosValues[7]);
        }

    }

    public static void TransformBlockAVX(Span<float> block)
    {
        Span<float> temp = stackalloc float[64];

        fixed (float* blockPtr = block)
        fixed (float* tempPtr = temp)
        {
            for (int row = 0; row < 8; row++)
            {
                float* rowPtr = blockPtr + row * 8;
                float* tempRowPtr = tempPtr + row * 8;

                Vector256<float> rowData = Avx.LoadVector256(rowPtr);
                Vector256<float> transformedRow = ButterflyWithinRow(rowData);

                Avx.Store(tempRowPtr, transformedRow);
            }
        }

        Span<float> trans = stackalloc float[64];
        Transpose8x8(temp, trans);

        fixed (float* transPtr = trans)
        fixed (float* tempPtr = temp)
        {
            for (int row = 0; row < 8; row++)
            {
                float* rowPtr = transPtr + row * 8;
                float* tempRowPtr = tempPtr + row * 8;

                Vector256<float> rowData = Avx.LoadVector256(rowPtr);
                Vector256<float> transformedRow = ButterflyWithinRow(rowData);

                Avx.Store(tempRowPtr, transformedRow);
            }
        }

        Transpose8x8(temp, block);

        for (var j = 0; j < 64; j++)
            block[j] *= 0.125f;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void Transpose8x8<T>(Span<T> src, Span<T> dst)
    {
        for (int i = 0; i < 8; i++)
            for (int j = 0; j < 8; j++)
                dst[j * 8 + i] = src[i * 8 + j];
    }

    private static Vector256<float> ButterflyWithinRow(Vector256<float> row)
    {
        // Input row: [x0, x1, x2, x3, x4, x5, x6, x7]

        // STAGE 1: 4 parallel butterflies on pairs
        Vector256<float> stage1 = ButterflyStage1(row);

        // STAGE 2: 2 parallel butterflies  
        Vector256<float> stage2 = ButterflyStage2(stage1);

        // STAGE 3: Final butterfly
        Vector256<float> stage3 = ButterflyStage3(stage2);

        return stage3;
    }

    private static Vector256<float> ButterflyStage1(Vector256<float> x)
    {
        // Butterfly pairs: (x0,x1), (x2,x3), (x4,x5), (x6,x7)

        // Separate even and odd elements
        Vector256<float> evens = Avx.Shuffle(x, x, 0x88); // [x0, x0, x2, x2, x4, x4, x6, x6]
        Vector256<float> odds = Avx.Shuffle(x, x, 0xDD);  // [x1, x1, x3, x3, x5, x5, x7, x7]

        // Load cosine constants for this stage
        Vector256<float> cos1 = CosConstants[0];

        // Butterfly operation: a + b*cos, a - b*cos
        Vector256<float> oddCos = Avx.Multiply(odds, cos1);

        Vector256<float> sum = Avx.Add(evens, oddCos);  // Upper butterfly outputs
        Vector256<float> diff = Avx.Subtract(evens, oddCos); // Lower butterfly outputs

        // Interleave results: [sum0, diff0, sum1, diff1, sum2, diff2, sum3, diff3]
        return Avx.UnpackLow(sum, diff);
    }

    private static Vector256<float> ButterflyStage2(Vector256<float> stage1)
    {
        // Input from Stage1: [s0, d0, s1, d1, s2, d2, s3, d3] = <-200, -200, 0, 0, 0, 0, 0, 0>

        // Rearrange: [s0, s2, s1, s3, d0, d2, d1, d3]  
        Vector256<float> permuted = Avx.Permute(stage1, 0xD8); // [-200, 0, 0, 0, -200, 0, 0, 0]

        // Separate into groups for butterfly
        Vector256<float> group1 = Avx.Shuffle(permuted, permuted, 0x88); // [-200, 0, -200, 0, -200, 0, -200, 0]
        Vector256<float> group2 = Avx.Shuffle(permuted, permuted, 0xDD); // [0, 0, 0, 0, 0, 0, 0, 0]

        // Load stage 2 constants
        Vector256<float> cos2 = Vector256.Create(1.0f, 0.0f, 1.0f, 0.0f, 1.0f, 0.0f, 1.0f, 0.0f); // [1, 0, 1, 0, 1, 0, 1, 0]

        // Complex multiplication: group2 * (cos2 + i*sin2)
        // real = group2_real * cos2 - group2_imag * sin2

        // For real inputs (imaginary parts are 0), this simplifies to:
        // real = group2_real * cos2

        Vector256<float> rotatedReal = Avx.Multiply(group2, cos2); // [0*1, 0*0, 0*1, 0*0, 0*1, 0*0, 0*1, 0*0] = [0,0,0,0,0,0,0,0]

        // Butterfly: group1 + rotated, group1 - rotated
        Vector256<float> sum = Avx.Add(group1, rotatedReal); // [-200, 0, -200, 0, -200, 0, -200, 0]
        Vector256<float> diff = Avx.Subtract(group1, rotatedReal); // [-200, 0, -200, 0, -200, 0, -200, 0]

        return Avx.UnpackLow(sum, diff); // [-200, -200, -200, -200, 0, 0, 0, 0]
    }

    //private static Vector256<float> ButterflyStage2(Vector256<float> stage1)
    //{
    //    // Input from Stage1: [s0, d0, s1, d1, s2, d2, s3, d3] = <-200, -200, 0, 0, 0, 0, 0, 0>

    //    // Rearrange to group for butterfly pairs: (s0,s2), (s1,s3), (d0,d2), (d1,d3)
    //    // Permute: [s0, s2, s1, s3, d0, d2, d1, d3] = [-200, 0, 0, 0, -200, 0, 0, 0]
    //    Vector256<float> permuted = Avx.Permute(stage1, 0b11011000); // 0xD8

    //    // Create upper and lower parts for butterfly
    //    // upper = [s0, s2, s0, s2, s1, s3, s1, s3] = [-200, 0, -200, 0, 0, 0, 0, 0]
    //    Vector256<float> upper = Avx.Shuffle(permuted, permuted, 0x44); // 01_00_01_00

    //    // lower = [d0, d2, d0, d2, d1, d3, d1, d3] = [-200, 0, -200, 0, 0, 0, 0, 0]  
    //    Vector256<float> lower = Avx.Shuffle(permuted, permuted, 0xEE); // 11_10_11_10

    //    // Create rotation factors for 45 degrees (0.7071 = cos(45°) = sin(45°))
    //    Vector256<float> rotationFactors = Vector256.Create(
    //        1.0f, 0.7071f, 1.0f, 0.7071f,  // For s0, s2 pairs
    //        1.0f, 0.7071f, 1.0f, 0.7071f   // For s1, s3 pairs
    //    );

    //    // Create sign factors for addition and subtraction
    //    Vector256<float> signFactors = Vector256.Create(
    //        1.0f, 1.0f, 1.0f, -1.0f,  // First: s0 + s2*0.7071, Second: s0 - s2*0.7071
    //        1.0f, 1.0f, 1.0f, -1.0f   // Same for s1, s3
    //    );

    //    // Perform the butterfly operation
    //    Vector256<float> multiplied = Avx.Multiply(upper, rotationFactors);
    //    Vector256<float> s_result = Avx.Multiply(multiplied, signFactors);

    //    Vector256<float> d_multiplied = Avx.Multiply(lower, rotationFactors);
    //    Vector256<float> d_result = Avx.Multiply(d_multiplied, signFactors);

    //    var result = Avx.HorizontalAdd(s_result, d_result);

    //    return result;
    //}

    //private static Vector256<float> ButterflyStage2(Vector256<float> stage1)
    //{
    //    // Input from Stage1: [s0, d0, s1, d1, s2, d2, s3, d3] = <-200, -200, 0, 0, 0, 0, 0, 0>

    //    // Rearrange to group for butterfly pairs: (s0,s2), (s1,s3), (d0,d2), (d1,d3)
    //    // Permute: [s0, s2, s1, s3, d0, d2, d1, d3] = [-200, 0, 0, 0, -200, 0, 0, 0]
    //    Vector256<float> permuted = Avx.Permute(stage1, 0b11011000); // 0xD8

    //    // Create upper and lower parts for butterfly
    //    // upper = [s0, s2, s0, s2, s1, s3, s1, s3] = [-200, 0, -200, 0, 0, 0, 0, 0]
    //    Vector256<float> upper = Avx.Shuffle(permuted, permuted, 0x44); // 01_00_01_00

    //    // lower = [d0, d2, d0, d2, d1, d3, d1, d3] = [-200, 0, -200, 0, 0, 0, 0, 0]  
    //    Vector256<float> lower = Avx.Shuffle(permuted, permuted, 0xEE); // 11_10_11_10

    //    // Stage 2 cosine constants (cos(π/4) = 0.70710678f)
    //    Vector256<float> cos2 = Vector256.Create(0.70710678f, 0.70710678f, 0.70710678f, 0.70710678f,
    //                                             0.70710678f, 0.70710678f, 0.70710678f, 0.70710678f);

    //    // Apply butterfly operations
    //    Vector256<float> lowerCos = Avx.Multiply(lower, cos2);
    //    Vector256<float> sum = Avx.Add(upper, lowerCos);   // s + d*cos
    //    Vector256<float> diff = Avx.Subtract(upper, lowerCos); // s - d*cos

    //    // Horizontal add to combine results
    //    // Result: [sum0+sum1, diff0+diff1, sum2+sum3, diff2+diff3, ...]
    //    return Avx.HorizontalAdd(sum, diff);
    //}

    //private static Vector256<float> ButterflyStage2(Vector256<float> stage1)
    //{
    //     Input from Stage1: [s0, d0, s1, d1, s2, d2, s3, d3]
    //     Where s0 = x0 + x1*cos, d0 = x0 - x1*cos, etc.

    //     Rearrange for 2 butterflies: (s0,s1) and (s2,s3), (d0,d1) and (d2,d3)

    //     Permute to group upper and lower parts
    //    Vector256<float> permuted = Avx.Permute(stage1, 0xD8); // [s0, s1, d0, d1, s2, s3, d2, d3]

    //     Separate upper and lower butterfly inputs
    //    Vector256<float> upper = Avx.Shuffle(permuted, permuted, 0x44); // [s0, s1, s0, s1, s2, s3, s2, s3]
    //    Vector256<float> lower = Avx.Shuffle(permuted, permuted, 0xEE); // [d0, d1, d0, d1, d2, d3, d2, d3]

    //     Load stage 2 cosine constants
    //    Vector256<float> cos2 = CosConstants[1];

    //     Apply butterfly operations
    //    Vector256<float> lowerCos = Avx.Multiply(lower, cos2);

    //    Vector256<float> sum = Avx.Add(upper, lowerCos);   // s0 + d0*cos, s1 + d1*cos, etc.
    //    Vector256<float> diff = Avx.Subtract(upper, lowerCos); // s0 - d0*cos, s1 - d1*cos, etc.

    //     Horizontal add to combine results: [sum0+sum1, diff0+diff1, sum2+sum3, diff2+diff3, ...]
    //    return Avx.HorizontalAdd(sum, diff);
    //}

    private static Vector256<float> ButterflyStage3(Vector256<float> stage2)
    {
        // The ExtractVector128/ToVector256 approach is inefficient
        // Better to use permutes and shuffles within the 256-bit vector

        Vector256<float> low = Avx.Permute2x128(stage2, stage2, 0x00); // [ss0, dd0, ss1, dd1]
        Vector256<float> high = Avx.Permute2x128(stage2, stage2, 0x11); // [ss2, dd2, ss3, dd3]

        // Use proper complex twiddle factors
        Vector256<float> cos3 = Vector256.Create(1.0f, 0.70710678f, 0.0f, -0.70710678f, 1.0f, 0.70710678f, 0.0f, -0.70710678f);
        Vector256<float> sin3 = Vector256.Create(0.0f, -0.70710678f, -1.0f, -0.70710678f, 0.0f, -0.70710678f, -1.0f, -0.70710678f);

        // Complex multiplication for high part
        Vector256<float> highReal = Avx.Subtract(
            Avx.Multiply(high, cos3),
            Avx.Multiply(Avx.Shuffle(high, high, 0xB1), sin3)
        );

        Vector256<float> sum = Avx.Add(low, highReal);
        Vector256<float> diff = Avx.Subtract(low, highReal);

        var result = Avx.Permute(Avx.UnpackLow(sum, diff), 0xD8);
        return result;
    }

    //private static Vector256<float> ButterflyStage3(Vector256<float> stage2)
    //{
    //    // Input from Stage2: [ss0, dd0, ss1, dd1, ss2, dd2, ss3, dd3]
    //    // Where ss0 = s0 + s1, dd0 = d0 + d1, etc.

    //    // Final stage: Process the remaining 2-point transforms

    //    // Extract low and high halves
    //    Vector128<float> low = Avx.ExtractVector128(stage2, 0);
    //    Vector128<float> high = Avx.ExtractVector128(stage2, 1);

    //    // Create vectors for final butterfly
    //    Vector256<float> vecLow = low.ToVector256();
    //    Vector256<float> vecHigh = high.ToVector256();

    //    // Load stage 3 cosine constants
    //    Vector256<float> cos3 = CosConstants[3];

    //    // Final butterfly: combine low and high parts
    //    Vector256<float> highCos = Avx.Multiply(vecHigh, cos3);

    //    Vector256<float> finalSum = Avx.Add(vecLow, highCos);
    //    Vector256<float> finalDiff = Avx.Subtract(vecLow, highCos);

    //    // Interleave the final results
    //    Vector256<float> interleaved = Avx.UnpackLow(finalSum, finalDiff);

    //    // Final permutation to get correct output order
    //    return Avx.Permute(interleaved, 0xD8);
    //}
}