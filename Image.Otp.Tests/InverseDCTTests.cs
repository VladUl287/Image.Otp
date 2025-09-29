using Image.Otp.Core.Helpers.Jpg;
using System.Diagnostics.CodeAnalysis;

namespace Image.Otp.Tests;

public class InverseDCTTests
{
    private const double Tolerance = 1e-10;

    [Fact]
    public void Idct8x8InPlace_WhenBlockIsNull_ThrowsArgumentNullException()
    {
        // Arrange
        double[] block = null;

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => block.Idct8x8InPlace());
    }

    [Fact]
    public void Idct8x8InPlace_WhenBlockLengthIsNot64_ThrowsArgumentException()
    {
        // Arrange
        double[] block = new double[63];

        // Act & Assert
        var exception = Assert.Throws<ArgumentException>(() => block.Idct8x8InPlace());
        Assert.Contains("Block must have exactly 64 elements", exception.Message);
    }

    [Fact]
    public void Idct8x8InPlace_WithAllZeros_ReturnsAllZeros()
    {
        // Arrange
        double[] block = new double[64];
        double[] expected = new double[64];

        // Act
        var result = block.Idct8x8InPlace();

        // Assert
        Assert.Equal(expected, result, new DoubleComparer(Tolerance));
        Assert.Same(block, result); // Should return same instance
    }

    [Fact]
    public void Idct8x8InPlace_WithDCCoefficientOnly_ReturnsConstantBlock()
    {
        // Arrange
        double[] block = new double[64];
        block[0] = 8.0; // DC coefficient (scaled by 8 for normalization)

        // Expected: all elements should be 1.0 since:
        // IDCT of DC-only block = constant value = DC/sqrt(2)*cos(0)*0.5 * 8 (for 8x8)
        double[] expected = new double[64];
        for (int i = 0; i < 64; i++)
        {
            expected[i] = 1.0;
        }

        // Act
        var result = block.Idct8x8InPlace();

        // Assert
        Assert.Equal(expected, result, new DoubleComparer(Tolerance));
    }

    [Fact]
    public void Idct8x8InPlace_WithSingleACCoefficient_ReturnsCosinePattern()
    {
        // Arrange
        double[] block = new double[64];
        int u = 1, v = 1; // AC coefficient at position (1,1)
        block[v * 8 + u] = 16.0; // Scale factor for visibility

        // Expected pattern should follow cos((2x+1)uπ/16) * cos((2y+1)vπ/16)
        double[] expected = new double[64];
        for (int y = 0; y < 8; y++)
        {
            for (int x = 0; x < 8; x++)
            {
                double cosX = Math.Cos(((2 * x + 1) * u * Math.PI) / 16.0);
                double cosY = Math.Cos(((2 * y + 1) * v * Math.PI) / 16.0);
                expected[y * 8 + x] = 0.5 * 0.5 * 16.0 * cosX * cosY; // 0.5 from each pass
            }
        }

        // Act
        var result = block.Idct8x8InPlace();

        // Assert
        Assert.Equal(expected, result, new DoubleComparer(Tolerance));
    }

    [Fact]
    public void Idct8x8InPlace_WithMultipleCoefficients_ReturnsCorrectTransformation()
    {
        // Arrange - test with DC + one AC component
        double[] block = new double[64];
        block[0] = 8.0;  // DC component
        block[9] = 4.0;  // AC component at (1,1)

        // Manually compute expected result using the same algorithm as the implementation
        double[] expected = new double[64];

        // First pass: 1D IDCT on rows (same as the implementation)
        double[] tmp = new double[64];
        for (int y = 0; y < 8; y++)
        {
            for (int x = 0; x < 8; x++)
            {
                double sum = 0.0;
                for (int u = 0; u < 8; u++)
                {
                    double coeff = u == 0 ? 1.0 / Math.Sqrt(2.0) : 1.0;
                    sum += coeff * block[y * 8 + u] * Math.Cos(((2 * x + 1) * u * Math.PI) / 16.0);
                }
                tmp[y * 8 + x] = sum * 0.5;
            }
        }

        // Second pass: 1D IDCT on columns (same as the implementation)
        for (int x = 0; x < 8; x++)
        {
            for (int y = 0; y < 8; y++)
            {
                double sum = 0.0;
                for (int v = 0; v < 8; v++)
                {
                    double coeff = v == 0 ? 1.0 / Math.Sqrt(2.0) : 1.0;
                    sum += coeff * tmp[v * 8 + x] * Math.Cos(((2 * y + 1) * v * Math.PI) / 16.0);
                }
                expected[y * 8 + x] = sum * 0.5;
            }
        }

        // Act
        var result = block.Idct8x8InPlace();

        // Assert
        Assert.Equal(expected, result, new DoubleComparer(Tolerance));
    }

    [Fact]
    public void Idct8x8InPlace_IsInPlaceOperation_ModifiesOriginalArray()
    {
        // Arrange
        double[] block = new double[64];
        block[0] = 8.0;
        double[] originalReference = block; // Keep reference to original array

        // Act
        var result = block.Idct8x8InPlace();

        // Assert
        Assert.Same(originalReference, result);
        Assert.Same(block, result);

        // Verify the array was modified (not all zeros)
        bool allZeros = true;
        for (int i = 0; i < 64; i++)
        {
            if (Math.Abs(result[i]) > Tolerance)
            {
                allZeros = false;
                break;
            }
        }
        Assert.False(allZeros);
    }

    [Fact]
    public void Idct8x8InPlace_WithMaxACCoefficient_ReturnsCorrectRange()
    {
        // Arrange - use maximum AC coefficient
        double[] block = new double[64];
        block[63] = 100.0; // Highest frequency component

        // Act
        var result = block.Idct8x8InPlace();

        // Assert - check that values are within reasonable range
        double maxValue = double.MinValue;
        double minValue = double.MaxValue;

        for (int i = 0; i < 64; i++)
        {
            if (result[i] > maxValue) maxValue = result[i];
            if (result[i] < minValue) minValue = result[i];
        }

        // High frequency components should produce values within expected range
        Assert.True(maxValue < 100.0); // Should be less than input coefficient
        Assert.True(minValue > -100.0); // Symmetric range
    }

    // Helper class for comparing double arrays with tolerance
    private class DoubleComparer(double tolerance) : System.Collections.Generic.IEqualityComparer<double[]>
    {
        public bool Equals(double[]? xArray, double[]? yArray)
        {
            if (xArray is not null && yArray is not null)
            {
                if (xArray.Length != yArray.Length) return false;

                for (int i = 0; i < xArray.Length; i++)
                {
                    if (Math.Abs(xArray[i] - yArray[i]) > tolerance)
                        return false;
                }
                return true;
            }
            return false;
        }

        public int GetHashCode([DisallowNull] double[] obj)
        {
            return obj?.GetHashCode() ?? 0;
        }
    }
}
