using Image.Otp.Core.Models.Jpeg;
using Image.Otp.Core.Utils;
using System.Buffers;

namespace Image.Otp.Core.Helpers;

public static class JpegHelpres
{
    public static double[] DequantizeBlock(short[] quantizedCoeffs, QuantizationTable qTable)
    {
        double[] outBlock = new double[64];
        for (int i = 0; i < 64; i++)
        {
            ushort qv = (i < qTable.Values.Length) ? qTable.Values[i] : (ushort)1;
            outBlock[i] = quantizedCoeffs[i] * (double)qv;
        }
        return outBlock;
    }

    public static void InverseDCT8x8(double[] block, double[] output)
    {
        // Implement basic separable IDCT using the naive formula for clarity.
        // This is slow but easy to read.
        double[] tmp = ArrayPool<double>.Shared.Rent(64);

        // Precompute basis factors
        static double c(int u) => u == 0 ? 1.0 / Math.Sqrt(2.0) : 1.0;

        // 1D IDCT on rows
        for (int y = 0; y < 8; y++)
        {
            for (int x = 0; x < 8; x++)
            {
                double sum = 0.0;
                for (int u = 0; u < 8; u++)
                {
                    sum += c(u) * block[y * 8 + u] * Math.Cos(((2 * x + 1) * u * Math.PI) / 16.0);
                }
                tmp[y * 8 + x] = sum * 0.5;
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
                    sum += c(v) * tmp[v * 8 + x] * Math.Cos(((2 * y + 1) * v * Math.PI) / 16.0);
                }
                output[y * 8 + x] = sum * 0.5;
            }
        }

        ArrayPool<double>.Shared.Return(tmp);
    }

    public static int DecodeHuffmanSymbol(JpegBitReader br, CanonicalHuffmanTable table)
    {
        var code = 0;
        for (var length = 1; length <= 16; length++)
        {
            var bit = br.ReadBit();
            if (bit < 0)
                return -1;

            code = (code << 1) | bit;

            if (table.TryGetSymbol(code, length, out var sym))
                return sym;
        }

        throw new InvalidDataException("Invalid Huffman code (no symbol within 16 bits).");
    }
}
