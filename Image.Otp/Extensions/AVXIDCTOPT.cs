using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics.X86;
using System.Runtime.Intrinsics;

namespace Image.Otp.Core.Extensions;
public unsafe class AVXIDCTOPT
{
    private const int BLOCK_SIZE = 64;

    public static void IDCT2D_llm_SIMD(Span<float> block)
    {
        Span<float> temp = stackalloc float[BLOCK_SIZE];

        for (var y = 0; y < 8; y++)
            IDCT1Dllm_32f_SIMD(block, y * 8, temp, y * 8);

        Span<float> trans = stackalloc float[BLOCK_SIZE];
        Transpose8x8(temp, trans);

        for (var j = 0; j < 8; j++)
            IDCT1Dllm_32f_SIMD(trans, j * 8, temp, j * 8);

        Transpose8x8(temp, block);

        for (var j = 0; j < BLOCK_SIZE; j++)
            block[j] *= 0.125f;
    }


    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void Transpose8x8<T>(Span<T> src, Span<T> dst)
    {
        for (int i = 0; i < 8; i++)
            for (int j = 0; j < 8; j++)
                dst[j * 8 + i] = src[i * 8 + j];
    }

    public static unsafe void IDCT1Dllm_32f_SIMD(ReadOnlySpan<float> y, int yOffset, Span<float> x, int xOffset)
    {
        // Load all 8 input elements into two vectors
        Vector256<float> row = Vector256.Create(y.Slice(yOffset, 8));

        // STAGE 1: Separate even and odd elements
        SeparateEvenOdd(row, out var evens, out var odds);

        // STAGE 2: Process odd elements (4-point transform)  
        Vector256<float> oddResult = ProcessOdd4Point(odds);

        // STAGE 3: Process even elements (4-point transform)
        Vector256<float> evenResult = ProcessEven4Point(evens);

        // STAGE 4: Final combination
        Vector256<float> resultPos = Avx.Add(evenResult, oddResult);
        Vector256<float> resultNeg = Avx.Subtract(evenResult, oddResult);

        // Store results in correct order
        StoreResults(resultPos, resultNeg, x, xOffset);
    }

    private static Vector256<float> ProcessOdd4Point(Vector256<float> odds)
    {
        // odds: [y1, y3, y5, y7, y1, y3, y5, y7] - we only need first 4

        // Create constants (float precision)
        Vector256<float> r1 = Vector256.Create(1.387040f);
        Vector256<float> r3 = Vector256.Create(1.175876f);
        Vector256<float> r5 = Vector256.Create(0.541196f);
        Vector256<float> r7 = Vector256.Create(0.275899f);

        // Negative zero for negation
        Vector256<float> negativeZero = Vector256.Create(-0.0f);

        // Extract individual odd elements by broadcasting
        Vector256<float> y1 = Avx.Shuffle(odds, odds, 0x00); // [y1, y1, y1, y1, y1, y1, y1, y1]
        Vector256<float> y3 = Avx.Shuffle(odds, odds, 0x55); // [y3, y3, y3, y3, y3, y3, y3, y3]
        Vector256<float> y5 = Avx.Shuffle(odds, odds, 0xAA); // [y5, y5, y5, y5, y5, y5, y5, y5]
        Vector256<float> y7 = Avx.Shuffle(odds, odds, 0xFF); // [y7, y7, y7, y7, y7, y7, y7, y7]

        // Compute intermediate values
        Vector256<float> z0 = Avx.Add(y1, y7);
        Vector256<float> z1 = Avx.Add(y3, y5);
        Vector256<float> z2 = Avx.Add(y3, y7);
        Vector256<float> z3 = Avx.Add(y1, y5);
        Vector256<float> z4 = Avx.Multiply(Avx.Add(z0, z1), r3);

        // Use XOR with negative zero for negation
        Vector256<float> neg_r1 = Avx.Xor(r1, negativeZero);
        Vector256<float> neg_r3 = Avx.Xor(r3, negativeZero);
        Vector256<float> neg_r5 = Avx.Xor(r5, negativeZero);
        Vector256<float> neg_r7 = Avx.Xor(r7, negativeZero);

        Vector256<float> z0_scaled = Avx.Multiply(z0, Avx.Add(neg_r3, r7));
        Vector256<float> z1_scaled = Avx.Multiply(z1, Avx.Add(neg_r3, neg_r1));
        Vector256<float> z2_scaled = Avx.Add(Avx.Multiply(z2, Avx.Add(neg_r3, neg_r5)), z4);
        Vector256<float> z3_scaled = Avx.Add(Avx.Multiply(z3, Avx.Add(neg_r3, r5)), z4);

        // Compute final odd results
        Vector256<float> b3 = Avx.Add(Avx.Add(
            Avx.Multiply(y7, Avx.Add(Avx.Add(neg_r1, r3), Avx.Add(r5, neg_r7))),
            z0_scaled), z2_scaled);

        Vector256<float> b2 = Avx.Add(Avx.Add(
            Avx.Multiply(y5, Avx.Add(Avx.Add(r1, r3), Avx.Add(r7, neg_r5))),
            z1_scaled), z3_scaled);

        Vector256<float> b1 = Avx.Add(Avx.Add(
            Avx.Multiply(y3, Avx.Add(Avx.Add(r1, r3), Avx.Add(r5, neg_r7))),
            z1_scaled), z2_scaled);

        Vector256<float> b0 = Avx.Add(Avx.Add(
            Avx.Multiply(y1, Avx.Add(Avx.Add(r1, r3), Avx.Add(neg_r5, neg_r7))),
            z0_scaled), z3_scaled);

        // Pack odd results: [b0, b1, b2, b3, b0, b1, b2, b3]
        Vector256<float> b01 = Avx.UnpackLow(b0, b1);
        Vector256<float> b23 = Avx.UnpackLow(b2, b3);
        return Avx.Shuffle(b01, b23, 0x44);
    }

    private static void SeparateEvenOdd(Vector256<float> input, out Vector256<float> evens, out Vector256<float> odds)
    {
        // Input: [y0, y1, y2, y3, y4, y5, y6, y7]

        odds = Vector256.Create(input[1], input[3], input[5], input[7], input[1], input[3], input[5], input[7]);  // Odd coefficients
        evens = Vector256.Create(input[0], input[2], input[4], input[6], input[0], input[2], input[4], input[6]);  // Even coefficients

        //// Create permutation masks
        //Vector256<float> low = Avx.Shuffle(input, input, 0x44); // [y0, y1, y4, y5, y0, y1, y4, y5]
        //Vector256<float> high = Avx.Shuffle(input, input, 0xEE); // [y2, y3, y6, y7, y2, y3, y6, y7]

        //// Separate evens: [y0, y2, y4, y6, y0, y2, y4, y6]
        //odds = Avx.Shuffle(low, high, 0x88);

        //// Separate odds: [y1, y3, y5, y7, y1, y3, y5, y7]  
        //evens = Avx.Shuffle(low, high, 0xDD);
    }

    private static Vector256<float> ProcessEven4Point(Vector256<float> evens)
    {
        // evens: [y0, y2, y4, y6, y0, y2, y4, y6] - we only need first 4

        // Constants for even part (float precision)
        Vector256<float> r6 = Vector256.Create(0.541196f); //r[6] = 0.541196
        Vector256<float> r2_plus_r6 = Vector256.Create(1.847759f); //r[2] + r[6] = 1.306563 + 0.541196 = 1.847759
        Vector256<float> r2_minus_r6 = Vector256.Create(0.765367f); //r[2] - r[6] = 1.306563 - 0.541196 = 0.765367

        // Extract individual even elements by broadcasting
        Vector256<float> y0 = Avx.Shuffle(evens, evens, 0x00); // [y0, y0, y0, y0, y0, y0, y0, y0]
        Vector256<float> y2 = Avx.Shuffle(evens, evens, 0x55); // [y2, y2, y2, y2, y2, y2, y2, y2]
        Vector256<float> y4 = Avx.Shuffle(evens, evens, 0xAA); // [y4, y4, y4, y4, y4, y4, y4, y4]
        Vector256<float> y6 = Avx.Shuffle(evens, evens, 0xFF); // [y6, y6, y6, y6, y6, y6, y6, y6]

        // Compute z4 = (y2 + y6) * r6
        Vector256<float> z4 = Avx.Multiply(Avx.Add(y2, y6), r6);

        // Compute z0 = y0 + y4, z1 = y0 - y4
        Vector256<float> z0 = Avx.Add(y0, y4);
        Vector256<float> z1 = Avx.Subtract(y0, y4);

        // Compute z2 = z4 - y6 * (r2 + r6)
        Vector256<float> z2 = Avx.Subtract(z4, Avx.Multiply(y6, r2_plus_r6));

        // Compute z3 = z4 + y2 * (r2 - r6)  
        Vector256<float> z3 = Avx.Add(z4, Avx.Multiply(y2, r2_minus_r6));

        // Final even results: a0 = z0 + z3, a1 = z1 + z2, a2 = z1 - z2, a3 = z0 - z3
        Vector256<float> a0 = Avx.Add(z0, z3);
        Vector256<float> a3 = Avx.Subtract(z0, z3);
        Vector256<float> a1 = Avx.Add(z1, z2);
        Vector256<float> a2 = Avx.Subtract(z1, z2);

        // Pack even results: [a0, a1, a2, a3, a0, a1, a2, a3]
        Vector256<float> a01 = Avx.UnpackLow(a0, a1);   // [a0, a1, a0, a1, a0, a1, a0, a1]
        Vector256<float> a23 = Avx.UnpackLow(a2, a3);   // [a2, a3, a2, a3, a2, a3, a2, a3]
        return Avx.Shuffle(a01, a23, 0x44);             // [a0, a1, a2, a3, a0, a1, a2, a3]
    }

    private static void StoreResults(Vector256<float> pos, Vector256<float> neg, Span<float> x, int xOffset)
    {
        // pos: [a0, a1, a2, a3, a0, a1, a2, a3]
        // neg: [b0, b1, b2, b3, b0, b1, b2, b3]

        Vector256<float> result = Avx.Permute2x128(pos, neg, 0x20);
        fixed (float* xPtr = &x[xOffset])
        {
            Avx.Store(xPtr, result);
        }
    }
}