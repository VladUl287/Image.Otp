using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics.X86;
using System.Runtime.Intrinsics;

namespace Image.Otp.Core.Extensions;

using FourRows = (Vector256<float>, Vector256<float>, Vector256<float>, Vector256<float>);

public unsafe class AVXIDCTOPT
{
    private const int BLOCK_SIZE = 64;

    public static void IDCT2D_SIMD_EIGHT_ROWS(Span<float> block)
    {
        Span<float> temp = stackalloc float[BLOCK_SIZE];

        var oddResults = ProcessOdd4Point_256(block);
        var evenResults = ProcessEven4Point_256(block);

        for (int row = 0; row < 4; row++)
        {
            Vector256<float> even = GetEvenRow(evenResults, row);
            Vector256<float> odd = GetOddRow(oddResults, row);

            Vector256<float> resultPos = Avx.Add(even, odd);
            Vector256<float> resultNeg = Avx.Subtract(even, odd);

            var outputStart = row * 16;
            StoreVector256(resultPos, temp[outputStart..]);
            StoreVector256(resultNeg, temp[(outputStart + 8)..]);
        }

        Span<float> trans = stackalloc float[BLOCK_SIZE];
        Transpose8x8(temp, trans);

        oddResults = ProcessOdd4Point_256(trans);
        evenResults = ProcessEven4Point_256(trans);

        for (int row = 0; row < 4; row++)
        {
            Vector256<float> even = GetEvenRow(evenResults, row);
            Vector256<float> odd = GetOddRow(oddResults, row);

            Vector256<float> resultPos = Avx.Add(even, odd);
            Vector256<float> resultNeg = Avx.Subtract(even, odd);

            var outputStart = row * 16;
            StoreVector256(resultPos, block[outputStart..]);
            StoreVector256(resultNeg, block[(outputStart + 8)..]);
        }

        for (var j = 0; j < BLOCK_SIZE; j++)
            block[j] *= 0.125f;
    }

    private static void StoreVector256(Vector256<float> vector, Span<float> destination)
    {
        var temp = stackalloc float[8];
        Avx.Store(temp, vector);

        for (int i = 0; i < 8; i++)
        {
            destination[i] = temp[i];
        }
    }

    private static void StoreRowToTemp(Span<float> temp, Vector256<float> firstHalf, Vector256<float> secondHalf, int row)
    {
        // Extract the 8 float values from the two vectors
        Span<float> firstHalfData = stackalloc float[8];
        Span<float> secondHalfData = stackalloc float[8];

        firstHalf.CopyTo(firstHalfData);
        secondHalf.CopyTo(secondHalfData);

        // Store first 4 points (positions 0-3)
        for (int i = 0; i < 4; i++)
            temp[row * 8 + i] = firstHalfData[i];

        // Store last 4 points (positions 4-7)  
        for (int i = 0; i < 4; i++)
            temp[row * 8 + i + 4] = secondHalfData[i];
    }

    private static Vector256<float> GetEvenRow(FourRows evenResults, int row)
    {
        return row switch
        {
            0 => evenResults.Item1,
            1 => evenResults.Item2,
            2 => evenResults.Item3,
            3 => evenResults.Item4,
            _ => throw new ArgumentOutOfRangeException(nameof(row))
        };
    }

    // Helper method to extract odd row data  
    private static Vector256<float> GetOddRow(FourRows oddResults, int row)
    {
        return row switch
        {
            0 => oddResults.Item1,
            1 => oddResults.Item2,
            2 => oddResults.Item3,
            3 => oddResults.Item4,
            4 => oddResults.Item1,
            5 => oddResults.Item2,
            6 => oddResults.Item3,
            7 => oddResults.Item4,
            _ => throw new ArgumentOutOfRangeException(nameof(row))
        };
    }

    private static FourRows ProcessOdd4Point_256(ReadOnlySpan<float> block)
    {
        // Load odd coefficients from different rows
        Vector256<float> y1 = Vector256.Create(
            block[1], block[9], block[17], block[25],
            block[33], block[41], block[49], block[57]);

        Vector256<float> y3 = Vector256.Create(
            block[3], block[11], block[19], block[27],
            block[35], block[43], block[51], block[59]);

        Vector256<float> y5 = Vector256.Create(
            block[5], block[13], block[21], block[29],
            block[37], block[45], block[53], block[61]);

        Vector256<float> y7 = Vector256.Create(
            block[7], block[15], block[23], block[31],
            block[39], block[47], block[55], block[63]);

        // Precomputed constants (float precision)
        Vector256<float> r1 = Vector256.Create(1.387040f);
        Vector256<float> r3 = Vector256.Create(1.175876f);
        Vector256<float> r5 = Vector256.Create(0.785695f);
        Vector256<float> r7 = Vector256.Create(0.275899f);

        // Compute intermediate values (same butterfly pattern as scalar)
        Vector256<float> z0 = Avx.Add(y1, y7);
        Vector256<float> z1 = Avx.Add(y3, y5);
        Vector256<float> z2 = Avx.Add(y3, y7);
        Vector256<float> z3 = Avx.Add(y1, y5);
        Vector256<float> z4 = Avx.Multiply(Avx.Add(z0, z1), r3);

        // Negative constants
        Vector256<float> negativeZero = Vector256.Create(-0.0f);
        Vector256<float> neg_r1 = Avx.Xor(r1, negativeZero);
        Vector256<float> neg_r3 = Avx.Xor(r3, negativeZero);
        Vector256<float> neg_r5 = Avx.Xor(r5, negativeZero);
        Vector256<float> neg_r7 = Avx.Xor(r7, negativeZero);

        Vector256<float> z0_scaled = Avx.Multiply(z0, Avx.Add(neg_r3, r7));
        Vector256<float> z1_scaled = Avx.Multiply(z1, Avx.Add(neg_r3, neg_r1));
        Vector256<float> z2_scaled = Avx.Add(Avx.Multiply(z2, Avx.Add(neg_r3, neg_r5)), z4);
        Vector256<float> z3_scaled = Avx.Add(Avx.Multiply(z3, Avx.Add(neg_r3, r5)), z4);

        Vector256<float> b3 = Avx.Add(Avx.Add(
            Avx.Multiply(y7, Avx.Add(Avx.Add(neg_r1, r3), Avx.Add(r5, neg_r7))),
            z0_scaled), z2_scaled);

        Vector256<float> b2 = Avx.Add(Avx.Add(
            Avx.Multiply(y5, Avx.Add(Avx.Add(r1, r3), Avx.Add(neg_r5, r7))),
            z1_scaled), z3_scaled);

        Vector256<float> b1 = Avx.Add(Avx.Add(
            Avx.Multiply(y3, Avx.Add(Avx.Add(r1, r3), Avx.Add(r5, neg_r7))),
            z1_scaled), z2_scaled);

        Vector256<float> b0 = Avx.Add(Avx.Add(
            Avx.Multiply(y1, Avx.Add(Avx.Add(r1, r3), Avx.Add(neg_r5, neg_r7))),
            z0_scaled), z3_scaled);

        var tmp0 = Avx.Shuffle(b0, b1, 0x44);
        var tmp1 = Avx.Shuffle(b0, b1, 0xEE);
        var tmp2 = Avx.Shuffle(b2, b3, 0x44);
        var tmp3 = Avx.Shuffle(b2, b3, 0xEE);

        return (
            Avx.Shuffle(tmp0, tmp2, 0x88),
            Avx.Shuffle(tmp0, tmp2, 0xDD),
            Avx.Shuffle(tmp1, tmp3, 0x88),
            Avx.Shuffle(tmp1, tmp3, 0xDD) 
        );
    }

    private static FourRows ProcessEven4Point_256(ReadOnlySpan<float> y)
    {
        // Load even coefficients from different rows
        Vector256<float> y0 = Vector256.Create(
            y[0],
            y[8],
            y[16],
            y[24],            
            y[32],
            y[40],
            y[48],
            y[56]);  // [y0_row0, y0_row1, y0_row2, y0_row3]

        Vector256<float> y2 = Vector256.Create(
            y[2],
            y[10],
            y[18],
            y[26],
            y[34],
            y[42],
            y[50],
            y[58]); // [y2_row0, y2_row1, y2_row2, y2_row3]

        Vector256<float> y4 = Vector256.Create(
            y[4],
            y[12],
            y[20],
            y[28],            
            y[36],
            y[44],
            y[52],
            y[60]); // [y4_row0, y4_row1, y4_row2, y4_row3]

        Vector256<float> y6 = Vector256.Create(
            y[6],
            y[14],
            y[22],
            y[30],
            y[38],
            y[46],
            y[54],
            y[62]); // [y6_row0, y6_row1, y6_row2, y6_row3]

        // Constants for even part (float precision)
        Vector256<float> r2 = Vector256.Create(1.306563f);
        Vector256<float> r6 = Vector256.Create(0.541196f);

        // Precomputed combinations
        Vector256<float> r2_plus_r6 = Vector256.Create(1.847759f); // r[2] + r[6]
        Vector256<float> r2_minus_r6 = Vector256.Create(0.765367f); // r[2] - r[6]

        // Compute z4 = (y2 + y6) * r6
        Vector256<float> z4 = Avx.Multiply(Avx.Add(y2, y6), r6);

        // Compute z0 = y0 + y4, z1 = y0 - y4
        Vector256<float> z0 = Avx.Add(y0, y4);
        Vector256<float> z1 = Avx.Subtract(y0, y4);

        // Compute z2 = z4 - y6 * (r2 + r6)
        Vector256<float> z2 = Avx.Subtract(z4, Avx.Multiply(y6, r2_plus_r6));

        // Compute z3 = z4 + y2 * (r2 - r6)
        Vector256<float> z3 = Avx.Add(z4, Avx.Multiply(y2, r2_minus_r6));

        // Final even results: [a0, a1, a2, a3] for each row
        Vector256<float> a0 = Avx.Add(z0, z3);
        Vector256<float> a3 = Avx.Subtract(z0, z3);
        Vector256<float> a1 = Avx.Add(z1, z2);
        Vector256<float> a2 = Avx.Subtract(z1, z2);

        // Transpose to get row-wise results
        var tmp0 = Avx.Shuffle(a0, a1, 0x44); // [a0_row0, a0_row1, a1_row0, a1_row1]
        var tmp1 = Avx.Shuffle(a0, a1, 0xEE); // [a0_row2, a0_row3, a1_row2, a1_row3]
        var tmp2 = Avx.Shuffle(a2, a3, 0x44); // [a2_row0, a2_row1, a3_row0, a3_row1]
        var tmp3 = Avx.Shuffle(a2, a3, 0xEE); // [a2_row2, a2_row3, a3_row2, a3_row3]

        return
        (
            Avx.Shuffle(tmp0, tmp2, 0x88), // [a0_row0, a1_row0, a2_row0, a3_row0] - Row 0
            Avx.Shuffle(tmp0, tmp2, 0xDD), // [a0_row1, a1_row1, a2_row1, a3_row1] - Row 1
            Avx.Shuffle(tmp1, tmp3, 0x88), // [a0_row2, a1_row2, a2_row2, a3_row2] - Row 2
            Avx.Shuffle(tmp1, tmp3, 0xDD)  // [a0_row3, a1_row3, a2_row3, a3_row3] - Row 3
        );
    }

    public static void IDCT2D_SIMD_FOUR_ROWS(Span<float> block)
    {
        Vector128<float>[] oddResults = [.. ProcessOdd4Point_128(block, 0), .. ProcessOdd4Point_128(block, 32)];
        Vector128<float>[] evenResults = [.. ProcessEven4Point_128(block, 0), .. ProcessEven4Point_128(block, 32)];

        Span<float> temp = stackalloc float[BLOCK_SIZE];

        for (int row = 0; row < 8; row++)
        {
            Vector128<float> resultPos = Sse.Add(evenResults[row], oddResults[row]);
            Vector128<float> resultNeg = Sse.Subtract(evenResults[row], oddResults[row]);

            int outputStart = row * 8;
            resultPos.CopyTo(temp[outputStart..]);
            resultNeg.CopyTo(temp[(outputStart + 4)..]);
        }

        Span<float> trans = stackalloc float[BLOCK_SIZE];
        Transpose8x8(temp, trans);

        oddResults = [.. ProcessOdd4Point_128(trans, 0), .. ProcessOdd4Point_128(trans, 32)];
        evenResults = [.. ProcessEven4Point_128(trans, 0), .. ProcessEven4Point_128(trans, 32)];

        for (int row = 0; row < 8; row++)
        {
            Vector128<float> resultPos = Sse.Add(evenResults[row], oddResults[row]);
            Vector128<float> resultNeg = Sse.Subtract(evenResults[row], oddResults[row]);

            var outputStart = row * 8;
            resultPos.CopyTo(block[outputStart..]);
            resultNeg.CopyTo(block[(outputStart + 4)..]);
        }

        for (var j = 0; j < BLOCK_SIZE; j++)
            block[j] *= 0.125f;
    }

    private static Vector128<float>[] ProcessOdd4Point_128(ReadOnlySpan<float> y, int baseIndex = 0)
    {
        // Load odd coefficients from different rows
        Vector128<float> y1 = Vector128.Create(
            y[baseIndex + 1], y[baseIndex + 9], y[baseIndex + 17], y[baseIndex + 25]);
        Vector128<float> y3 = Vector128.Create(
            y[baseIndex + 3], y[baseIndex + 11], y[baseIndex + 19], y[baseIndex + 27]);
        Vector128<float> y5 = Vector128.Create(
            y[baseIndex + 5], y[baseIndex + 13], y[baseIndex + 21], y[baseIndex + 29]);
        Vector128<float> y7 = Vector128.Create(
            y[baseIndex + 7], y[baseIndex + 15], y[baseIndex + 23], y[baseIndex + 31]);

        // Precomputed constants (float precision)
        Vector128<float> r1 = Vector128.Create(1.387040f);
        Vector128<float> r3 = Vector128.Create(1.175876f);
        Vector128<float> r5 = Vector128.Create(0.785695f);  // Note: corrected from 0.541196
        Vector128<float> r7 = Vector128.Create(0.275899f);

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

        var tmp0 = Sse.Shuffle(b0, b1, 0x44); // [b0_row0, b0_row1, b1_row0, b1_row1]
        var tmp1 = Sse.Shuffle(b0, b1, 0xEE); // [b0_row2, b0_row3, b1_row2, b1_row3]
        var tmp2 = Sse.Shuffle(b2, b3, 0x44); // [b2_row0, b2_row1, b3_row0, b3_row1]
        var tmp3 = Sse.Shuffle(b2, b3, 0xEE); // [b2_row2, b2_row3, b3_row2, b3_row3]

        return
        [
            Sse.Shuffle(tmp0, tmp2, 0x88), // [b0_row0, b1_row0, b2_row0, b3_row0]
            Sse.Shuffle(tmp0, tmp2, 0xDD), // [b0_row1, b1_row1, b2_row1, b3_row1]
            Sse.Shuffle(tmp1, tmp3, 0x88), // [b0_row2, b1_row2, b2_row2, b3_row2]
            Sse.Shuffle(tmp1, tmp3, 0xDD)  // [b0_row3, b1_row3, b2_row3, b3_row3]
        ];
    }

    private static Vector128<float>[] ProcessEven4Point_128(ReadOnlySpan<float> y, int baseIndex = 0)
    {
        // Load even coefficients from different rows
        Vector128<float> y0 = Vector128.Create(
            y[baseIndex + 0],
            y[baseIndex + 8],
            y[baseIndex + 16],
            y[baseIndex + 24]);  // [y0_row0, y0_row1, y0_row2, y0_row3]

        Vector128<float> y2 = Vector128.Create(
            y[baseIndex + 2],
            y[baseIndex + 10],
            y[baseIndex + 18],
            y[baseIndex + 26]); // [y2_row0, y2_row1, y2_row2, y2_row3]

        Vector128<float> y4 = Vector128.Create(
            y[baseIndex + 4],
            y[baseIndex + 12],
            y[baseIndex + 20],
            y[baseIndex + 28]); // [y4_row0, y4_row1, y4_row2, y4_row3]

        Vector128<float> y6 = Vector128.Create(
            y[baseIndex + 6],
            y[baseIndex + 14],
            y[baseIndex + 22],
            y[baseIndex + 30]); // [y6_row0, y6_row1, y6_row2, y6_row3]

        // Constants for even part (float precision)
        Vector128<float> r2 = Vector128.Create(1.306563f);
        Vector128<float> r6 = Vector128.Create(0.541196f);

        // Precomputed combinations
        Vector128<float> r2_plus_r6 = Vector128.Create(1.847759f); // r[2] + r[6]
        Vector128<float> r2_minus_r6 = Vector128.Create(0.765367f); // r[2] - r[6]

        // Compute z4 = (y2 + y6) * r6
        Vector128<float> z4 = Sse.Multiply(Sse.Add(y2, y6), r6);

        // Compute z0 = y0 + y4, z1 = y0 - y4
        Vector128<float> z0 = Sse.Add(y0, y4);
        Vector128<float> z1 = Sse.Subtract(y0, y4);

        // Compute z2 = z4 - y6 * (r2 + r6)
        Vector128<float> z2 = Sse.Subtract(z4, Sse.Multiply(y6, r2_plus_r6));

        // Compute z3 = z4 + y2 * (r2 - r6)
        Vector128<float> z3 = Sse.Add(z4, Sse.Multiply(y2, r2_minus_r6));

        // Final even results: [a0, a1, a2, a3] for each row
        Vector128<float> a0 = Sse.Add(z0, z3);
        Vector128<float> a3 = Sse.Subtract(z0, z3);
        Vector128<float> a1 = Sse.Add(z1, z2);
        Vector128<float> a2 = Sse.Subtract(z1, z2);

        // Transpose to get row-wise results
        var tmp0 = Sse.Shuffle(a0, a1, 0x44); // [a0_row0, a0_row1, a1_row0, a1_row1]
        var tmp1 = Sse.Shuffle(a0, a1, 0xEE); // [a0_row2, a0_row3, a1_row2, a1_row3]
        var tmp2 = Sse.Shuffle(a2, a3, 0x44); // [a2_row0, a2_row1, a3_row0, a3_row1]
        var tmp3 = Sse.Shuffle(a2, a3, 0xEE); // [a2_row2, a2_row3, a3_row2, a3_row3]

        return
        [
            Sse.Shuffle(tmp0, tmp2, 0x88), // [a0_row0, a1_row0, a2_row0, a3_row0] - Row 0
            Sse.Shuffle(tmp0, tmp2, 0xDD), // [a0_row1, a1_row1, a2_row1, a3_row1] - Row 1
            Sse.Shuffle(tmp1, tmp3, 0x88), // [a0_row2, a1_row2, a2_row2, a3_row2] - Row 2
            Sse.Shuffle(tmp1, tmp3, 0xDD)  // [a0_row3, a1_row3, a2_row3, a3_row3] - Row 3
        ];
    }

    public static void IDCT2D_SIMD_SSE(Span<float> block)
    {
        Span<float> temp = stackalloc float[BLOCK_SIZE];

        for (var y = 0; y < 8; y++)
            IDCT1Dllm_32f_SIMD_Sse(block.Slice(y * 8, 8), temp.Slice(y * 8, 8));

        Span<float> trans = stackalloc float[BLOCK_SIZE];
        Transpose8x8(temp, trans);

        for (var j = 0; j < 8; j++)
            IDCT1Dllm_32f_SIMD_Sse(trans.Slice(j * 8, 8), temp.Slice(j * 8, 8));

        Transpose8x8(temp, block);

        for (var j = 0; j < BLOCK_SIZE; j++)
            block[j] *= 0.125f;
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

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void Transpose8x8<T>(Span<T> src, Span<T> dst)
    {
        for (int i = 0; i < 8; i++)
            for (int j = 0; j < 8; j++)
                dst[j * 8 + i] = src[i * 8 + j];
    }


    //public static unsafe void IDCT1Dllm_32f_SIMD(ReadOnlySpan<float> y, int yOffset, Span<float> x, int xOffset)
    //{
    //    // Load all 8 input elements into two vectors
    //    //Vector256<float> row = Vector256.Create(y.Slice(yOffset, 8));
    //    Vector256<float> row;
    //    fixed (float* yPtr = &y[yOffset])
    //    {
    //        row = Avx.LoadVector256(yPtr); // Direct load is faster
    //    }

    //    // STAGE 1: Separate even and odd elements
    //    SeparateEvenOdd(row, out var evens, out var odds);

    //    // STAGE 2: Process odd elements (4-point transform)  
    //    Vector256<float> oddResult = ProcessOdd4Point(odds);

    //    // STAGE 3: Process even elements (4-point transform)
    //    Vector256<float> evenResult = ProcessEven4Point(evens);

    //    // STAGE 4: Final combination
    //    Vector256<float> resultPos = Avx.Add(evenResult, oddResult);
    //    Vector256<float> resultNeg = Avx.Subtract(evenResult, oddResult);

    //    // Store results in correct order
    //    //Vector256<float> result = Avx.Permute2x128(resultPos, resultNeg, 0x20);
    //    //result.CopyTo(x.Slice(xOffset, 8));
    //    fixed (float* xPtr = &x[xOffset])
    //    {
    //        Avx.Store(xPtr, Avx.Permute2x128(resultPos, resultNeg, 0x20));
    //    }
    //}

    //private static Vector256<float> ProcessOdd4Point(Vector256<float> odds)
    //{
    //    // odds: [y1, y3, y5, y7, y1, y3, y5, y7] - we only need first 4

    //    // Create constants (float precision)
    //    Vector256<float> r1 = Vector256.Create(1.387040f);
    //    Vector256<float> r3 = Vector256.Create(1.175876f);
    //    Vector256<float> r5 = Vector256.Create(0.541196f);
    //    Vector256<float> r7 = Vector256.Create(0.275899f);

    //    // Negative zero for negation
    //    Vector256<float> negativeZero = Vector256.Create(-0.0f);

    //    // Extract individual odd elements by broadcasting
    //    Vector256<float> y1 = Avx.Shuffle(odds, odds, 0x00); // [y1, y1, y1, y1, y1, y1, y1, y1]
    //    Vector256<float> y3 = Avx.Shuffle(odds, odds, 0x55); // [y3, y3, y3, y3, y3, y3, y3, y3]
    //    Vector256<float> y5 = Avx.Shuffle(odds, odds, 0xAA); // [y5, y5, y5, y5, y5, y5, y5, y5]
    //    Vector256<float> y7 = Avx.Shuffle(odds, odds, 0xFF); // [y7, y7, y7, y7, y7, y7, y7, y7]

    //    // Compute intermediate values
    //    Vector256<float> z0 = Avx.Add(y1, y7);
    //    Vector256<float> z1 = Avx.Add(y3, y5);
    //    Vector256<float> z2 = Avx.Add(y3, y7);
    //    Vector256<float> z3 = Avx.Add(y1, y5);
    //    Vector256<float> z4 = Avx.Multiply(Avx.Add(z0, z1), r3);

    //    // Use XOR with negative zero for negation
    //    Vector256<float> neg_r1 = Avx.Xor(r1, negativeZero);
    //    Vector256<float> neg_r3 = Avx.Xor(r3, negativeZero);
    //    Vector256<float> neg_r5 = Avx.Xor(r5, negativeZero);
    //    Vector256<float> neg_r7 = Avx.Xor(r7, negativeZero);

    //    Vector256<float> z0_scaled = Avx.Multiply(z0, Avx.Add(neg_r3, r7));
    //    Vector256<float> z1_scaled = Avx.Multiply(z1, Avx.Add(neg_r3, neg_r1));
    //    Vector256<float> z2_scaled = Avx.Add(Avx.Multiply(z2, Avx.Add(neg_r3, neg_r5)), z4);
    //    Vector256<float> z3_scaled = Avx.Add(Avx.Multiply(z3, Avx.Add(neg_r3, r5)), z4);

    //    // Compute final odd results
    //    Vector256<float> b3 = Avx.Add(Avx.Add(
    //        Avx.Multiply(y7, Avx.Add(Avx.Add(neg_r1, r3), Avx.Add(r5, neg_r7))),
    //        z0_scaled), z2_scaled);

    //    Vector256<float> b2 = Avx.Add(Avx.Add(
    //        Avx.Multiply(y5, Avx.Add(Avx.Add(r1, r3), Avx.Add(r7, neg_r5))),
    //        z1_scaled), z3_scaled);

    //    Vector256<float> b1 = Avx.Add(Avx.Add(
    //        Avx.Multiply(y3, Avx.Add(Avx.Add(r1, r3), Avx.Add(r5, neg_r7))),
    //        z1_scaled), z2_scaled);

    //    Vector256<float> b0 = Avx.Add(Avx.Add(
    //        Avx.Multiply(y1, Avx.Add(Avx.Add(r1, r3), Avx.Add(neg_r5, neg_r7))),
    //        z0_scaled), z3_scaled);

    //    // Pack odd results: [b0, b1, b2, b3, b0, b1, b2, b3]
    //    Vector256<float> b01 = Avx.UnpackLow(b0, b1);
    //    Vector256<float> b23 = Avx.UnpackLow(b2, b3);
    //    return Avx.Shuffle(b01, b23, 0x44);
    //}

    //private static Vector256<float> ProcessEven4Point(Vector256<float> evens)
    //{
    //    // evens: [y0, y2, y4, y6, y0, y2, y4, y6] - we only need first 4

    //    // Constants for even part (float precision)
    //    Vector256<float> r6 = Vector256.Create(0.541196f); //r[6] = 0.541196
    //    Vector256<float> r2_plus_r6 = Vector256.Create(1.847759f); //r[2] + r[6] = 1.306563 + 0.541196 = 1.847759
    //    Vector256<float> r2_minus_r6 = Vector256.Create(0.765367f); //r[2] - r[6] = 1.306563 - 0.541196 = 0.765367

    //    // Extract individual even elements by broadcasting
    //    Vector256<float> y0 = Avx.Shuffle(evens, evens, 0x00); // [y0, y0, y0, y0, y0, y0, y0, y0]
    //    Vector256<float> y2 = Avx.Shuffle(evens, evens, 0x55); // [y2, y2, y2, y2, y2, y2, y2, y2]
    //    Vector256<float> y4 = Avx.Shuffle(evens, evens, 0xAA); // [y4, y4, y4, y4, y4, y4, y4, y4]
    //    Vector256<float> y6 = Avx.Shuffle(evens, evens, 0xFF); // [y6, y6, y6, y6, y6, y6, y6, y6]

    //    // Compute z4 = (y2 + y6) * r6
    //    Vector256<float> z4 = Avx.Multiply(Avx.Add(y2, y6), r6);

    //    // Compute z0 = y0 + y4, z1 = y0 - y4
    //    Vector256<float> z0 = Avx.Add(y0, y4);
    //    Vector256<float> z1 = Avx.Subtract(y0, y4);

    //    // Compute z2 = z4 - y6 * (r2 + r6)
    //    Vector256<float> z2 = Avx.Subtract(z4, Avx.Multiply(y6, r2_plus_r6));

    //    // Compute z3 = z4 + y2 * (r2 - r6)  
    //    Vector256<float> z3 = Avx.Add(z4, Avx.Multiply(y2, r2_minus_r6));

    //    // Final even results: a0 = z0 + z3, a1 = z1 + z2, a2 = z1 - z2, a3 = z0 - z3
    //    Vector256<float> a0 = Avx.Add(z0, z3);
    //    Vector256<float> a3 = Avx.Subtract(z0, z3);
    //    Vector256<float> a1 = Avx.Add(z1, z2);
    //    Vector256<float> a2 = Avx.Subtract(z1, z2);

    //    // Pack even results: [a0, a1, a2, a3, a0, a1, a2, a3]
    //    Vector256<float> a01 = Avx.UnpackLow(a0, a1);   // [a0, a1, a0, a1, a0, a1, a0, a1]
    //    Vector256<float> a23 = Avx.UnpackLow(a2, a3);   // [a2, a3, a2, a3, a2, a3, a2, a3]
    //    return Avx.Shuffle(a01, a23, 0x44);             // [a0, a1, a2, a3, a0, a1, a2, a3]
    //}

    //[MethodImpl(MethodImplOptions.AggressiveInlining)]
    //private static void SeparateEvenOdd(Vector256<float> row, out Vector256<float> evens, out Vector256<float> odds)
    //{
    //    // Input: [y0, y1, y2, y3, y4, y5, y6, y7]

    //    //odds = Vector256.Create(row[1], row[3], row[5], row[7], row[1], row[3], row[5], row[7]);  // Odd coefficients
    //    //evens = Vector256.Create(row[0], row[2], row[4], row[6], row[0], row[2], row[4], row[6]);  // Even coefficients

    //    var low = Avx.Shuffle(row, row, 0x44);  // [y0, y1, y4, y5, y0, y1, y4, y5]
    //    var high = Avx.Shuffle(row, row, 0xEE); // [y2, y3, y6, y7, y2, y3, y6, y7]

    //    evens = Avx.Shuffle(low, high, 0x88); // [y0, y2, y4, y6, y0, y2, y4, y6]
    //    odds = Avx.Shuffle(low, high, 0xDD);  // [y1, y3, y5, y7, y1, y3, y5, y7]
    //}
}