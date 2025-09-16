namespace Image.Otp.Models.Jpeg;

//public static class JpegIdct
//{
//    private static readonly int[] ZigZag = new int[] {
//         0, 1, 5, 6,14,15,27,28,
//         2, 4, 7,13,16,26,29,42,
//         3, 8,12,17,25,30,41,43,
//         9,11,18,24,31,40,44,53,
//        10,19,23,32,39,45,52,54,
//        20,22,33,38,46,51,55,60,
//        21,34,37,47,50,56,59,61,
//        35,36,48,49,57,58,62,63
//    };

//    // Dequantize (coeffs in zig order) -> inverse DCT -> return 64 bytes (natural order), rounding exactly as tests expect.
//    public static byte[] DequantizeInverseZigZagIdct(short[] coeffsZig, ushort[] quantTableNatural)
//    {
//        if (coeffsZig == null) throw new ArgumentNullException(nameof(coeffsZig));
//        if (quantTableNatural == null) throw new ArgumentNullException(nameof(quantTableNatural));
//        if (coeffsZig.Length != 64) throw new ArgumentException("coeffsZig must be length 64", nameof(coeffsZig));
//        if (quantTableNatural.Length != 64) throw new ArgumentException("quantTableNatural must be length 64", nameof(quantTableNatural));

//        // 1) Dequantize into natural-order F[u*8+v]
//        double[] F = new double[64]; // natural order (u row major)
//        for (int i = 0; i < 64; i++)
//        {
//            int nat = ZigZag[i]; // natural index corresponding to zig index i
//            F[nat] = coeffsZig[i] * (double)quantTableNatural[nat];
//        }

//        // 2) Inverse DCT (floating point) -> raw spatial samples (no +128), match normalization used by tests
//        double[] raw = new double[64];
//        for (int y = 0; y < 8; y++)
//        {
//            for (int x = 0; x < 8; x++)
//            {
//                double sum = 0.0;
//                for (int u = 0; u < 8; u++)
//                {
//                    double cu = (u == 0) ? 1.0 / Math.Sqrt(2.0) : 1.0;
//                    for (int v = 0; v < 8; v++)
//                    {
//                        double cv = (v == 0) ? 1.0 / Math.Sqrt(2.0) : 1.0;
//                        double Fuv = F[u * 8 + v];
//                        // note: use same cos expressions and normalization as test's InverseIDCTFromDequant
//                        double cos1 = Math.Cos((2 * x + 1) * u * Math.PI / 16.0);
//                        double cos2 = Math.Cos((2 * y + 1) * v * Math.PI / 16.0);
//                        sum += cu * cv * Fuv * cos1 * cos2;
//                    }
//                }
//                raw[y * 8 + x] = sum / 4.0;
//            }
//        }

//        // 3) Add 128, round to nearest using AwayFromZero (to match test's Math.Round with AwayFromZero),
//        //    clamp to 0..255 and return as bytes in natural order
//        byte[] outBytes = new byte[64];
//        for (int i = 0; i < 64; i++)
//        {
//            double v = raw[i] + 128.0;
//            // Round to nearest, ties away from zero to match test expectation
//            int rounded = (int)Math.Round(v, MidpointRounding.AwayFromZero);
//            if (rounded < 0) rounded = 0;
//            else if (rounded > 255) rounded = 255;
//            outBytes[i] = (byte)rounded;
//        }

//        return outBytes;
//    }
//}

public static class JpegIdct
{
    static readonly int[] ZigZag = {
         0, 1, 5, 6,14,15,27,28,
         2, 4, 7,13,16,26,29,42,
         3, 8,12,17,25,30,41,43,
         9,11,18,24,31,40,44,53,
        10,19,23,32,39,45,52,54,
        20,22,33,38,46,51,55,60,
        21,34,37,47,50,56,59,61,
        35,36,48,49,57,58,62,63
    };

    public static byte[] DequantizeInverseZigZagIdct(short[] coeffs, ushort[] quantTable)
    {
        //Dequantize
        double[] dequantized = new double[64];
        for (int i = 0; i < 64; i++)
        {
            dequantized[i] = coeffs[i] * quantTable[i];
        }

        // Example logging
        //Console.WriteLine("Dequantized Values:");
        //foreach (var val in dequantized)
        //{
        //    Console.Write(val + ", ");
        //}

        // Apply IDCT with proper normalization
        double[] output = new double[64];

        for (int y = 0; y < 8; y++)
        {
            for (int x = 0; x < 8; x++)
            {
                double sum = 0.0;

                for (int u = 0; u < 8; u++)
                {
                    for (int v = 0; v < 8; v++)
                    {
                        double cu = u == 0 ? 1.0 / Math.Sqrt(2) : 1.0;
                        double cv = v == 0 ? 1.0 / Math.Sqrt(2) : 1.0;

                        sum += cu * cv * dequantized[u * 8 + v] *
                               Math.Cos((2 * x + 1) * u * Math.PI / 16.0) *
                               Math.Cos((2 * y + 1) * v * Math.PI / 16.0);
                    }
                }

                output[y * 8 + x] = sum / 4.0;
            }
        }

        // Convert to bytes with proper clamping and level shifting
        byte[] result = new byte[64];
        for (int i = 0; i < 64; i++)
        {
            double value = output[i] + 128.0;
            result[i] = (byte)Math.Max(0, Math.Min(255, (int)Math.Round(value)));
        }

        //DumpBlockStages("tag 1", coeffs, dequantized, output, result);

        return result;
    }

    static void DumpBlockStages(string tag, short[] qCoeffs, double[] dequant, byte[] idctPixels, byte[] finalRgb)
    {
        Console.WriteLine(tag);
        Console.WriteLine(" qCoeffs: " + string.Join(",", qCoeffs.Take(16)));
        Console.WriteLine(" dequant: " + string.Join(",", dequant.Take(16)));
        Console.WriteLine(" idct Y: " + string.Join(",", idctPixels.Take(8)));
        Console.WriteLine(" outB: " + string.Join(",", finalRgb.Take(16)));
    }

    // input: short[64] freq coefficients (dequantized), output: byte[64] samples (0..255)
    static byte[] IdctFloat(double[] f)
    {
        double[,] F = new double[8, 8];
        for (int i = 0; i < 64; i++) F[i / 8, i % 8] = f[i];

        double[,] tmp = new double[8, 8];
        double[,] outp = new double[8, 8];
        const double PI = Math.PI;

        // 1D IDCT on rows
        for (int y = 0; y < 8; y++)
        {
            for (int x = 0; x < 8; x++)
            {
                double sum = 0.0;
                for (int u = 0; u < 8; u++)
                {
                    double cu = (u == 0) ? (1.0 / Math.Sqrt(2.0)) : 1.0;
                    sum += cu * F[y, u] * Math.Cos((2 * x + 1) * u * PI / 16.0);
                }
                tmp[y, x] = sum * 0.5;
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
                    double cv = (v == 0) ? (1.0 / Math.Sqrt(2.0)) : 1.0;
                    sum += cv * tmp[v, x] * Math.Cos((2 * y + 1) * v * PI / 16.0);
                }
                outp[y, x] = 0.5 * sum;
            }
        }

        byte[] res = new byte[64];
        for (int i = 0; i < 64; i++)
        {
            // add 128 level shift and clamp
            int val = (int)Math.Round(outp[i / 8, i % 8] + 128.0);
            res[i] = (byte)Math.Clamp(val, 0, 255);
        }
        return res;
    }

}