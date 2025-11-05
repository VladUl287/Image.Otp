using System.Runtime.Intrinsics.X86;
using System.Runtime.Intrinsics;

namespace Image.Otp.Core.Extensions;

public unsafe static class IDCT_AVX
{
    private const int BLOCK_SIZE = 64;

    private static readonly Vector256<float> R1 = Vector256.Create(1.387040f);
    private static readonly Vector256<float> R3 = Vector256.Create(1.175876f);
    private static readonly Vector256<float> R5 = Vector256.Create(0.785695f);
    private static readonly Vector256<float> R7 = Vector256.Create(0.275899f);
    private static readonly Vector256<float> R2 = Vector256.Create(1.306563f);
    private static readonly Vector256<float> R6 = Vector256.Create(0.541196f);
    private static readonly Vector256<float> R2_PLUS_R6 = Vector256.Create(1.847759f);
    private static readonly Vector256<float> R1_PLUS_R3 = Vector256.Create(2.562916f);
    private static readonly Vector256<float> R2_MINUS_R6 = Vector256.Create(0.765367f);
    private static readonly Vector256<float> SCALE = Vector256.Create(0.125f);

    private static readonly Vector256<float> NEGATIVE_ZERO = Vector256.Create(-0.0f);
    private static readonly Vector256<float> NEG_R1 = Avx.Xor(R1, NEGATIVE_ZERO);
    private static readonly Vector256<float> NEG_R3 = Avx.Xor(R3, NEGATIVE_ZERO);
    private static readonly Vector256<float> NEG_R5 = Avx.Xor(R5, NEGATIVE_ZERO);
    private static readonly Vector256<float> NEG_R7 = Avx.Xor(R7, NEGATIVE_ZERO);

    private static readonly Vector256<int> Y0_INDICES = Vector256.Create(0, 8, 16, 24, 32, 40, 48, 56);
    private static readonly Vector256<int> Y1_INDICES = Vector256.Create(1, 9, 17, 25, 33, 41, 49, 57);
    private static readonly Vector256<int> Y2_INDICES = Vector256.Create(2, 10, 18, 26, 34, 42, 50, 58);
    private static readonly Vector256<int> Y3_INDICES = Vector256.Create(3, 11, 19, 27, 35, 43, 51, 59);
    private static readonly Vector256<int> Y4_INDICES = Vector256.Create(4, 12, 20, 28, 36, 44, 52, 60);
    private static readonly Vector256<int> Y5_INDICES = Vector256.Create(5, 13, 21, 29, 37, 45, 53, 61);
    private static readonly Vector256<int> Y6_INDICES = Vector256.Create(6, 14, 22, 30, 38, 46, 54, 62);
    private static readonly Vector256<int> Y7_INDICES = Vector256.Create(7, 15, 23, 31, 39, 47, 55, 63);

    public static void IDCT2D_AVX(Span<float> block)
    {
        fixed (float* blockPtr = block)
        {
            float* tempPtr = stackalloc float[BLOCK_SIZE];

            IDCT1D_AVX(blockPtr, tempPtr, 1f);
            IDCT1D_AVX(tempPtr, blockPtr, 0.125f);
        }
    }

    private static void IDCT1D_AVX(float* b, float* output, float scale)
    {
        Vector256<float> y0, y1, y2, y3, y4, y5, y6, y7;

        if (Avx2.IsSupported)
        {
            const byte float_bytes_size = 4; //sizeof(float)
            y0 = Avx2.GatherVector256(b, Y0_INDICES, float_bytes_size);
            y1 = Avx2.GatherVector256(b, Y1_INDICES, float_bytes_size);
            y2 = Avx2.GatherVector256(b, Y2_INDICES, float_bytes_size);
            y3 = Avx2.GatherVector256(b, Y3_INDICES, float_bytes_size);
            y4 = Avx2.GatherVector256(b, Y4_INDICES, float_bytes_size);
            y5 = Avx2.GatherVector256(b, Y5_INDICES, float_bytes_size);
            y6 = Avx2.GatherVector256(b, Y6_INDICES, float_bytes_size);
            y7 = Avx2.GatherVector256(b, Y7_INDICES, float_bytes_size);
        }
        else
        {
            y0 = Vector256.Create(b[0], b[8], b[16], b[24], b[32], b[40], b[48], b[56]);
            y1 = Vector256.Create(b[1], b[9], b[17], b[25], b[33], b[41], b[49], b[57]);
            y2 = Vector256.Create(b[2], b[10], b[18], b[26], b[34], b[42], b[50], b[58]);
            y3 = Vector256.Create(b[3], b[11], b[19], b[27], b[35], b[43], b[51], b[59]);
            y4 = Vector256.Create(b[4], b[12], b[20], b[28], b[36], b[44], b[52], b[60]);
            y5 = Vector256.Create(b[5], b[13], b[21], b[29], b[37], b[45], b[53], b[61]);
            y6 = Vector256.Create(b[6], b[14], b[22], b[30], b[38], b[46], b[54], b[62]);
            y7 = Vector256.Create(b[7], b[15], b[23], b[31], b[39], b[47], b[55], b[63]);
        }

        // Process EVEN part (y0, y2, y4, y6)
        var z4_even = Avx.Multiply(Avx.Add(y2, y6), R6);
        var z0_even = Avx.Add(y0, y4);
        var z1_even = Avx.Subtract(y0, y4);
        var z2_even = Avx.Subtract(z4_even, Avx.Multiply(y6, R2_PLUS_R6));
        var z3_even = Avx.Add(z4_even, Avx.Multiply(y2, R2_MINUS_R6));

        var a0 = Avx.Add(z0_even, z3_even);
        var a3 = Avx.Subtract(z0_even, z3_even);
        var a1 = Avx.Add(z1_even, z2_even);
        var a2 = Avx.Subtract(z1_even, z2_even);

        // Process ODD part (y1, y3, y5, y7)
        var z0_odd = Avx.Add(y1, y7);
        var z1_odd = Avx.Add(y3, y5);
        var z2_odd = Avx.Add(y3, y7);
        var z3_odd = Avx.Add(y1, y5);
        var z4_odd = Avx.Multiply(Avx.Add(z0_odd, z1_odd), R3);

        var z0_scaled = Avx.Multiply(z0_odd, Avx.Add(NEG_R3, R7));
        var z1_scaled = Avx.Multiply(z1_odd, Avx.Add(NEG_R3, NEG_R1));
        var z2_scaled = Avx.Add(Avx.Multiply(z2_odd, Avx.Add(NEG_R3, NEG_R5)), z4_odd);
        var z3_scaled = Avx.Add(Avx.Multiply(z3_odd, Avx.Add(NEG_R3, R5)), z4_odd);

        var b0 = Avx.Add(Avx.Add(
            Avx.Multiply(y1, Avx.Add(R1_PLUS_R3, Avx.Add(NEG_R5, NEG_R7))),
            z0_scaled), z3_scaled);

        var b1 = Avx.Add(Avx.Add(
            Avx.Multiply(y3, Avx.Add(R1_PLUS_R3, Avx.Add(R5, NEG_R7))),
            z1_scaled), z2_scaled);

        var b2 = Avx.Add(Avx.Add(
            Avx.Multiply(y5, Avx.Add(R1_PLUS_R3, Avx.Add(NEG_R5, R7))),
            z1_scaled), z3_scaled);

        var b3 = Avx.Add(Avx.Add(
            Avx.Multiply(y7, Avx.Add(R1_PLUS_R3, Avx.Add(R5, NEG_R7))),
            z0_scaled), z2_scaled);

        var scaleX = Vector256.Create(scale);
        var x0 = Avx.Multiply(Avx.Add(a0, b0), scaleX);
        var x1 = Avx.Multiply(Avx.Add(a1, b1), scaleX);
        var x2 = Avx.Multiply(Avx.Add(a2, b2), scaleX);
        var x3 = Avx.Multiply(Avx.Add(a3, b3), scaleX);
        var x4 = Avx.Multiply(Avx.Subtract(a3, b3), scaleX);
        var x5 = Avx.Multiply(Avx.Subtract(a2, b2), scaleX);
        var x6 = Avx.Multiply(Avx.Subtract(a1, b1), scaleX);
        var x7 = Avx.Multiply(Avx.Subtract(a0, b0), scaleX);

        Avx.Store(output + 0, x0);
        Avx.Store(output + 8, x1);
        Avx.Store(output + 16, x2);
        Avx.Store(output + 24, x3);
        Avx.Store(output + 32, x4);
        Avx.Store(output + 40, x5);
        Avx.Store(output + 48, x6);
        Avx.Store(output + 56, x7);
    }
}