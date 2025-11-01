using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics.X86;
using System.Runtime.Intrinsics;

namespace Image.Otp.Core.Extensions;
public unsafe class AVXIDCTOPT
{
    private const int BLOCK_SIZE = 64;

    public static unsafe void IDCT2D_SIMD_G(Span<float> block)
    {
        for (int i = 0; i < 8; i += 4)
        {
            IDCT4Rows_SIMD(block, i);
        }
    }

    private static unsafe void IDCT4Rows_SIMD(Span<float> block, int startRow)
    {
        // We'll process 4 rows simultaneously by interleaving their coefficients
        // This uses ALL SIMD lanes efficiently

        // Constants (precomputed once, could be static readonly)
        Vector256<float> r1 = Vector256.Create(1.387040f);
        Vector256<float> r3 = Vector256.Create(1.175876f);
        Vector256<float> r5 = Vector256.Create(0.785695f);
        Vector256<float> r7 = Vector256.Create(0.275899f);
        Vector256<float> r2 = Vector256.Create(1.306563f);
        Vector256<float> r6 = Vector256.Create(0.541196f);
        Vector256<float> r2_plus_r6 = Vector256.Create(1.847759f);
        Vector256<float> r2_minus_r6 = Vector256.Create(0.765367f);
        Vector256<float> negativeZero = Vector256.Create(-0.0f);

        fixed (float* blockPtr = block)
        {
            // Load and interleave 4 rows - each vector contains the same coefficient from 4 different rows
            Vector256<float>[] y = new Vector256<float>[8];

            for (int k = 0; k < 8; k++)
            {
                // Load coefficient k from 4 consecutive rows
                // [row0_coefk, row1_coefk, row2_coefk, row3_coefk, row0_coefk, row1_coefk, row2_coefk, row3_coefk]
                Vector128<float> coeffs0 = Sse.LoadVector128(blockPtr + (startRow + 0) * 8 + k);
                Vector128<float> coeffs1 = Sse.LoadVector128(blockPtr + (startRow + 1) * 8 + k);
                Vector128<float> coeffs2 = Sse.LoadVector128(blockPtr + (startRow + 2) * 8 + k);
                Vector128<float> coeffs3 = Sse.LoadVector128(blockPtr + (startRow + 3) * 8 + k);

                // Interleave to get: [row0, row1, row2, row3, row0, row1, row2, row3] for this coefficient
                Vector128<float> interleaved01 = Sse.UnpackLow(coeffs0, coeffs1);
                Vector128<float> interleaved23 = Sse.UnpackLow(coeffs2, coeffs3);

                // Create 256-bit vector: [r0, r1, r2, r3, r0, r1, r2, r3]
                Vector128<float> packed = Sse.MoveLowToHigh(coeffs0, coeffs1); // [r0, r1, X, X]
                Vector128<float> packed2 = Sse.MoveLowToHigh(coeffs2, coeffs3); // [r2, r3, X, X]
                Vector256<float> combined = Avx.InsertVector128(Vector256<float>.Zero, packed, 0);
                combined = Avx.InsertVector128(combined, packed2, 1); // [r0, r1, r2, r3, 0, 0, 0, 0]

                // Duplicate to fill all lanes
                y[k] = Avx.Permute2x128(combined, combined, 0x00); // [r0, r1, r2, r3, r0, r1, r2, r3]
            }

            // Process ODD coefficients for all 4 rows simultaneously
            Vector256<float> z0 = Avx.Add(y[1], y[7]);
            Vector256<float> z1 = Avx.Add(y[3], y[5]);
            Vector256<float> z2 = Avx.Add(y[3], y[7]);
            Vector256<float> z3 = Avx.Add(y[1], y[5]);
            Vector256<float> z4 = Avx.Multiply(Avx.Add(z0, z1), r3);

            Vector256<float> neg_r1 = Avx.Xor(r1, negativeZero);
            Vector256<float> neg_r3 = Avx.Xor(r3, negativeZero);
            Vector256<float> neg_r5 = Avx.Xor(r5, negativeZero);
            Vector256<float> neg_r7 = Avx.Xor(r7, negativeZero);

            Vector256<float> z0_scaled = Avx.Multiply(z0, Avx.Add(neg_r3, r7));
            Vector256<float> z1_scaled = Avx.Multiply(z1, Avx.Add(neg_r3, neg_r1));
            Vector256<float> z2_scaled = Avx.Add(Avx.Multiply(z2, Avx.Add(neg_r3, neg_r5)), z4);
            Vector256<float> z3_scaled = Avx.Add(Avx.Multiply(z3, Avx.Add(neg_r3, r5)), z4);

            Vector256<float> b3 = Avx.Add(Avx.Add(
                Avx.Multiply(y[7], Avx.Add(Avx.Add(neg_r1, r3), Avx.Add(r5, neg_r7))),
                z0_scaled), z2_scaled);

            Vector256<float> b2 = Avx.Add(Avx.Add(
                Avx.Multiply(y[5], Avx.Add(Avx.Add(r1, r3), Avx.Add(neg_r5, r7))),
                z1_scaled), z3_scaled);

            Vector256<float> b1 = Avx.Add(Avx.Add(
                Avx.Multiply(y[3], Avx.Add(Avx.Add(r1, r3), Avx.Add(r5, neg_r7))),
                z1_scaled), z2_scaled);

            Vector256<float> b0 = Avx.Add(Avx.Add(
                Avx.Multiply(y[1], Avx.Add(Avx.Add(r1, r3), Avx.Add(neg_r5, neg_r7))),
                z0_scaled), z3_scaled);

            // Process EVEN coefficients for all 4 rows simultaneously
            Vector256<float> z4_even = Avx.Multiply(Avx.Add(y[2], y[6]), r6);
            Vector256<float> z0_even = Avx.Add(y[0], y[4]);
            Vector256<float> z1_even = Avx.Subtract(y[0], y[4]);
            Vector256<float> z2_even = Avx.Subtract(z4_even, Avx.Multiply(y[6], r2_plus_r6));
            Vector256<float> z3_even = Avx.Add(z4_even, Avx.Multiply(y[2], r2_minus_r6));

            Vector256<float> a0 = Avx.Add(z0_even, z3_even);
            Vector256<float> a3 = Avx.Subtract(z0_even, z3_even);
            Vector256<float> a1 = Avx.Add(z1_even, z2_even);
            Vector256<float> a2 = Avx.Subtract(z1_even, z2_even);

            // Final combination for all 4 rows
            Vector256<float>[] x = new Vector256<float>[8];
            x[0] = Avx.Add(a0, b0);
            x[7] = Avx.Subtract(a0, b0);
            x[1] = Avx.Add(a1, b1);
            x[6] = Avx.Subtract(a1, b1);
            x[2] = Avx.Add(a2, b2);
            x[5] = Avx.Subtract(a2, b2);
            x[3] = Avx.Add(a3, b3);
            x[4] = Avx.Subtract(a3, b3);

            // Store results back - deinterleave the 4 rows
            for (int k = 0; k < 8; k++)
            {
                Vector256<float> result = x[k];
                // Deinterleave back to separate rows
                Vector128<float> resultLow = Avx.ExtractVector128(result, 0);
                Vector128<float> resultHigh = Avx.ExtractVector128(result, 1);

                Vector128<float> row01 = Sse.UnpackLow(resultLow, resultHigh);
                Vector128<float> row23 = Sse.UnpackHigh(resultLow, resultHigh);

                Vector128<float> row0 = Sse.Shuffle(row01, row01, 0x88);
                Vector128<float> row1 = Sse.Shuffle(row01, row01, 0xDD);
                Vector128<float> row2 = Sse.Shuffle(row23, row23, 0x88);
                Vector128<float> row3 = Sse.Shuffle(row23, row23, 0xDD);

                Sse.Store(blockPtr + (startRow + 0) * 8 + k, row0);
                Sse.Store(blockPtr + (startRow + 1) * 8 + k, row1);
                Sse.Store(blockPtr + (startRow + 2) * 8 + k, row2);
                Sse.Store(blockPtr + (startRow + 3) * 8 + k, row3);
            }
        }
    }

    public static void IDCT2D_SIMD(Span<float> block)
    {
        Span<float> temp = stackalloc float[BLOCK_SIZE];

        for (var y = 0; y < 8; y++)
            //IDCT1Dllm_32f_SIMD(block, y * 8, temp, y * 8);
            IDCT1Dllm_32f_SIMD_Sse(block.Slice(y * 8, 8), temp.Slice(y * 8, 8));

        Span<float> trans = stackalloc float[BLOCK_SIZE];
        Transpose8x8(temp, trans);

        for (var j = 0; j < 8; j++)
            //IDCT1Dllm_32f_SIMD(trans, j * 8, temp, j * 8);
            IDCT1Dllm_32f_SIMD_Sse(trans.Slice(j * 8, 8), temp.Slice(j * 8, 8));

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

    public static void IDCT1Dllm_32f_SIMD_Sse(ReadOnlySpan<float> y, Span<float> x)
    {
        // Load all 8 coefficients as two 128-bit vectors
        Vector128<float> all = Vector128.Create(y); // [y0,y1,y2,y3]
        Vector128<float> all2 = Vector128.Create(y[4..]); // [y4,y5,y6,y7]

        // Separate evens/odds efficiently
        Vector128<float> evens = Sse.Shuffle(all, all2, 0x88); // [y0,y2,y4,y6]
        Vector128<float> odds = Sse.Shuffle(all, all2, 0xDD);  // [y1,y3,y5,y7]

        // Process 4-point transforms with 128-bit vectors
        Vector128<float> oddResult = ProcessOdd4Point_128(odds);
        Vector128<float> evenResult = ProcessEven4Point_128(evens);

        // Final combination
        Vector128<float> resultPos = Sse.Add(evenResult, oddResult);
        Vector128<float> resultNeg = Sse.Subtract(evenResult, oddResult);

        // Store efficiently
        resultPos.CopyTo(x);
        resultNeg.CopyTo(x[4..]);
    }

    private static Vector128<float> ProcessOdd4Point_128(Vector128<float> odds)
    {
        // odds: [y1, y3, y5, y7]

        // Precomputed constants (float precision)
        Vector128<float> r1 = Vector128.Create(1.387040f);
        Vector128<float> r3 = Vector128.Create(1.175876f);
        Vector128<float> r5 = Vector128.Create(0.785695f);  // Note: corrected from 0.541196
        Vector128<float> r7 = Vector128.Create(0.275899f);

        // Extract individual elements - much more efficient than 256-bit version
        Vector128<float> y1 = Sse.Shuffle(odds, odds, 0x00); // [y1, y1, y1, y1]
        Vector128<float> y3 = Sse.Shuffle(odds, odds, 0x55); // [y3, y3, y3, y3]
        Vector128<float> y5 = Sse.Shuffle(odds, odds, 0xAA); // [y5, y5, y5, y5]
        Vector128<float> y7 = Sse.Shuffle(odds, odds, 0xFF); // [y7, y7, y7, y7]

        // Compute intermediate values (same butterfly pattern as scalar)
        Vector128<float> z0 = Sse.Add(y1, y7);
        Vector128<float> z1 = Sse.Add(y3, y5);
        Vector128<float> z2 = Sse.Add(y3, y7);
        Vector128<float> z3 = Sse.Add(y1, y5);
        Vector128<float> z4 = Sse.Multiply(Sse.Add(z0, z1), r3);

        // Negative constants
        Vector128<float> negativeZero = Vector128.Create(-0.0f);
        Vector128<float> neg_r1 = Sse.Xor(r1, negativeZero);
        Vector128<float> neg_r3 = Sse.Xor(r3, negativeZero);
        Vector128<float> neg_r5 = Sse.Xor(r5, negativeZero);
        Vector128<float> neg_r7 = Sse.Xor(r7, negativeZero);

        Vector128<float> z0_scaled = Sse.Multiply(z0, Sse.Add(neg_r3, r7));
        Vector128<float> z1_scaled = Sse.Multiply(z1, Sse.Add(neg_r3, neg_r1));
        Vector128<float> z2_scaled = Sse.Add(Sse.Multiply(z2, Sse.Add(neg_r3, neg_r5)), z4);
        Vector128<float> z3_scaled = Sse.Add(Sse.Multiply(z3, Sse.Add(neg_r3, r5)), z4);

        // Compute final odd results [b0, b1, b2, b3]
        Vector128<float> b3 = Sse.Add(Sse.Add(
            Sse.Multiply(y7, Sse.Add(Sse.Add(neg_r1, r3), Sse.Add(r5, neg_r7))),
            z0_scaled), z2_scaled);

        Vector128<float> b2 = Sse.Add(Sse.Add(
            Sse.Multiply(y5, Sse.Add(Sse.Add(r1, r3), Sse.Add(neg_r5, r7))),
            z1_scaled), z3_scaled);

        Vector128<float> b1 = Sse.Add(Sse.Add(
            Sse.Multiply(y3, Sse.Add(Sse.Add(r1, r3), Sse.Add(r5, neg_r7))),
            z1_scaled), z2_scaled);

        Vector128<float> b0 = Sse.Add(Sse.Add(
            Sse.Multiply(y1, Sse.Add(Sse.Add(r1, r3), Sse.Add(neg_r5, neg_r7))),
            z0_scaled), z3_scaled);

        // Pack results: [b0, b1, b2, b3]
        Vector128<float> b01 = Sse.UnpackLow(b0, b1);   // [b0, b1, b0, b1]
        Vector128<float> b23 = Sse.UnpackLow(b2, b3);   // [b2, b3, b2, b3]
        return Sse.Shuffle(b01, b23, 0x44);             // [b0, b1, b2, b3]
    }

    private static Vector128<float> ProcessEven4Point_128(Vector128<float> evens)
    {
        // evens: [y0, y2, y4, y6]

        // Constants for even part (float precision)
        Vector128<float> r2 = Vector128.Create(1.306563f);
        Vector128<float> r6 = Vector128.Create(0.541196f);

        // Precomputed combinations
        Vector128<float> r2_plus_r6 = Vector128.Create(1.847759f); // r[2] + r[6]
        Vector128<float> r2_minus_r6 = Vector128.Create(0.765367f); // r[2] - r[6]

        // Extract individual even elements
        Vector128<float> y0 = Sse.Shuffle(evens, evens, 0x00); // [y0, y0, y0, y0]
        Vector128<float> y2 = Sse.Shuffle(evens, evens, 0x55); // [y2, y2, y2, y2]
        Vector128<float> y4 = Sse.Shuffle(evens, evens, 0xAA); // [y4, y4, y4, y4]
        Vector128<float> y6 = Sse.Shuffle(evens, evens, 0xFF); // [y6, y6, y6, y6]

        // Compute z4 = (y2 + y6) * r6
        Vector128<float> z4 = Sse.Multiply(Sse.Add(y2, y6), r6);

        // Compute z0 = y0 + y4, z1 = y0 - y4
        Vector128<float> z0 = Sse.Add(y0, y4);
        Vector128<float> z1 = Sse.Subtract(y0, y4);

        // Compute z2 = z4 - y6 * (r2 + r6)
        Vector128<float> z2 = Sse.Subtract(z4, Sse.Multiply(y6, r2_plus_r6));

        // Compute z3 = z4 + y2 * (r2 - r6)
        Vector128<float> z3 = Sse.Add(z4, Sse.Multiply(y2, r2_minus_r6));

        // Final even results: [a0, a1, a2, a3]
        Vector128<float> a0 = Sse.Add(z0, z3);
        Vector128<float> a3 = Sse.Subtract(z0, z3);
        Vector128<float> a1 = Sse.Add(z1, z2);
        Vector128<float> a2 = Sse.Subtract(z1, z2);

        // Pack results: [a0, a1, a2, a3]
        Vector128<float> a01 = Sse.UnpackLow(a0, a1);   // [a0, a1, a0, a1]
        Vector128<float> a23 = Sse.UnpackLow(a2, a3);   // [a2, a3, a2, a3]
        return Sse.Shuffle(a01, a23, 0x44);             // [a0, a1, a2, a3]
    }

    public static unsafe void IDCT1Dllm_32f_SIMD(ReadOnlySpan<float> y, int yOffset, Span<float> x, int xOffset)
    {
        // Load all 8 input elements into two vectors
        //Vector256<float> row = Vector256.Create(y.Slice(yOffset, 8));
        Vector256<float> row;
        fixed (float* yPtr = &y[yOffset])
        {
            row = Avx.LoadVector256(yPtr); // Direct load is faster
        }

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
        //Vector256<float> result = Avx.Permute2x128(resultPos, resultNeg, 0x20);
        //result.CopyTo(x.Slice(xOffset, 8));
        fixed (float* xPtr = &x[xOffset])
        {
            Avx.Store(xPtr, Avx.Permute2x128(resultPos, resultNeg, 0x20));
        }
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

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void SeparateEvenOdd(Vector256<float> row, out Vector256<float> evens, out Vector256<float> odds)
    {
        // Input: [y0, y1, y2, y3, y4, y5, y6, y7]

        //odds = Vector256.Create(row[1], row[3], row[5], row[7], row[1], row[3], row[5], row[7]);  // Odd coefficients
        //evens = Vector256.Create(row[0], row[2], row[4], row[6], row[0], row[2], row[4], row[6]);  // Even coefficients

        var low = Avx.Shuffle(row, row, 0x44);  // [y0, y1, y4, y5, y0, y1, y4, y5]
        var high = Avx.Shuffle(row, row, 0xEE); // [y2, y3, y6, y7, y2, y3, y6, y7]

        evens = Avx.Shuffle(low, high, 0x88); // [y0, y2, y4, y6, y0, y2, y4, y6]
        odds = Avx.Shuffle(low, high, 0xDD);  // [y1, y3, y5, y7, y1, y3, y5, y7]
    }

    public sealed class IDCTConstants
    {
        public static IDCTConstants Instance { get; } = new IDCTConstants();

        public readonly Vector256<float> R1;
        public readonly Vector256<float> R3;
        public readonly Vector256<float> R5;
        public readonly Vector256<float> R7;
        public readonly Vector256<float> R6;
        public readonly Vector256<float> R2_Plus_R6;
        public readonly Vector256<float> R2_Minus_R6;
        public readonly Vector256<float> NegativeZero;

        private IDCTConstants()
        {
            R1 = Vector256.Create(1.387040f);
            R3 = Vector256.Create(1.175876f);
            R5 = Vector256.Create(0.541196f);
            R7 = Vector256.Create(0.275899f);
            R6 = Vector256.Create(0.541196f);
            R2_Plus_R6 = Vector256.Create(1.847759f);
            R2_Minus_R6 = Vector256.Create(0.765367f);
            NegativeZero = Vector256.Create(-0.0f);
        }
    }
}