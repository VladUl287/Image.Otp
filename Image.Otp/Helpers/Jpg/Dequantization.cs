using System.Numerics;

namespace Image.Otp.Core.Helpers.Jpg;

public static class Dequantization
{
    private const int BLOCK_SIZE = 64;

    public static double[] DequantizeInPlace(this double[] coeffs, double[] qTable)
    {
        ArgumentNullException.ThrowIfNull(coeffs, nameof(coeffs));
        ArgumentNullException.ThrowIfNull(qTable, nameof(qTable));

        if (coeffs.Length != BLOCK_SIZE || qTable.Length != BLOCK_SIZE)
            throw new ArgumentException("Arrays must have 64 elements");

        var vectorSize = Vector<double>.Count;
        var i = 0;

        while (i <= BLOCK_SIZE - vectorSize)
        {
            var vCoeffs = new Vector<double>(coeffs, i);
            var vQTable = new Vector<double>(qTable, i);

            var result = vCoeffs * vQTable;
            result.CopyTo(coeffs, i);

            i += vectorSize;
        }

        while (i < BLOCK_SIZE)
        {
            coeffs[i] *= qTable[i];
            i++;
        }

        return coeffs;
    }

    public static Span<double> DequantizeInPlace(this Span<double> coeffs, double[] qTable)
    {
        ArgumentNullException.ThrowIfNull(qTable, nameof(qTable));

        if (coeffs.Length != BLOCK_SIZE || qTable.Length != BLOCK_SIZE)
            throw new ArgumentException("Arrays must have 64 elements");

        var vectorSize = Vector<double>.Count;
        var i = 0;

        while (i <= BLOCK_SIZE - vectorSize)
        {
            var vCoeffs = new Vector<double>(coeffs[i..]);
            var vQTable = new Vector<double>(qTable, i);

            var result = vCoeffs * vQTable;
            result.CopyTo(coeffs[i..]);

            i += vectorSize;
        }

        while (i < BLOCK_SIZE)
        {
            coeffs[i] *= qTable[i];
            i++;
        }

        return coeffs;
    }
}
