using Image.Otp.Helpers.Jpg;

namespace Image.Otp.Tests;

public class DequantizationTests
{
    private const int BLOCK_SIZE = 64;

    [Fact]
    public void DequantizeInPlace_WithValid64ElementArrays_ModifiesArrayCorrectly()
    {
        // Arrange
        double[] coeffs = new double[BLOCK_SIZE];
        double[] qTable = new double[BLOCK_SIZE];
        double[] expected = new double[BLOCK_SIZE];

        // Fill arrays with test data
        for (int i = 0; i < BLOCK_SIZE; i++)
        {
            coeffs[i] = i + 1; // 1, 2, 3, ..., 64
            qTable[i] = 2.0;
            expected[i] = (i + 1) * 2.0;
        }

        // Act
        double[] result = coeffs.DequantizeInPlace(qTable);

        // Assert
        Assert.Same(coeffs, result); // Should return the same reference
        for (int i = 0; i < BLOCK_SIZE; i++)
        {
            Assert.Equal(expected[i], coeffs[i], 10);
        }
    }

    [Fact]
    public void DequantizeInPlace_WithVectorOptimization_HandlesAllElementsCorrectly()
    {
        // Arrange
        double[] coeffs = new double[BLOCK_SIZE];
        double[] qTable = new double[BLOCK_SIZE];

        // Use pattern that tests vector boundaries
        for (int i = 0; i < BLOCK_SIZE; i++)
        {
            coeffs[i] = i % 8 + 1; // Pattern: 1-8 repeating
            qTable[i] = i % 4 + 1; // Pattern: 1-4 repeating
        }

        // Calculate expected results manually
        double[] expected = new double[BLOCK_SIZE];
        for (int i = 0; i < BLOCK_SIZE; i++)
        {
            expected[i] = coeffs[i] * qTable[i];
        }

        // Act
        double[] result = coeffs.DequantizeInPlace(qTable);

        // Assert - verify all elements are processed correctly
        for (int i = 0; i < BLOCK_SIZE; i++)
        {
            Assert.Equal(expected[i], result[i], 10);
        }
    }

    [Fact]
    public void DequantizeInPlace_WithCoeffsLessThan64Elements_ThrowsArgumentException()
    {
        // Arrange
        double[] coeffs = new double[63]; // One less than BLOCK_SIZE
        double[] qTable = new double[BLOCK_SIZE];

        // Act & Assert
        var exception = Assert.Throws<ArgumentException>(() => coeffs.DequantizeInPlace(qTable));
        Assert.Contains("64 elements", exception.Message);
    }

    [Fact]
    public void DequantizeInPlace_WithQTableLessThan64Elements_ThrowsArgumentException()
    {
        // Arrange
        double[] coeffs = new double[BLOCK_SIZE];
        double[] qTable = new double[63]; // One less than BLOCK_SIZE

        // Act & Assert
        var exception = Assert.Throws<ArgumentException>(() => coeffs.DequantizeInPlace(qTable));
        Assert.Contains("64 elements", exception.Message);
    }

    [Fact]
    public void DequantizeInPlace_WithBothArraysLessThan64Elements_ThrowsArgumentException()
    {
        // Arrange
        double[] coeffs = new double[50];
        double[] qTable = new double[50];

        // Act & Assert
        Assert.Throws<ArgumentException>(() => coeffs.DequantizeInPlace(qTable));
    }

    [Fact]
    public void DequantizeInPlace_WithCoeffsMoreThan64Elements_ThrowsArgumentException()
    {
        // Arrange
        double[] coeffs = new double[65]; // One more than BLOCK_SIZE
        double[] qTable = new double[BLOCK_SIZE];

        // Act & Assert
        Assert.Throws<ArgumentException>(() => coeffs.DequantizeInPlace(qTable));
    }

    [Fact]
    public void DequantizeInPlace_WithQTableMoreThan64Elements_ThrowsArgumentException()
    {
        // Arrange
        double[] coeffs = new double[BLOCK_SIZE];
        double[] qTable = new double[65]; // One more than BLOCK_SIZE

        // Act & Assert
        Assert.Throws<ArgumentException>(() => coeffs.DequantizeInPlace(qTable));
    }

    [Fact]
    public void DequantizeInPlace_WithNullCoeffs_ThrowsArgumentNullException()
    {
        // Arrange
        double[] coeffs = null;
        double[] qTable = new double[BLOCK_SIZE];

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => coeffs.DequantizeInPlace(qTable));
    }

    [Fact]
    public void DequantizeInPlace_WithNullQTable_ThrowsArgumentNullException()
    {
        // Arrange
        double[] coeffs = new double[BLOCK_SIZE];
        double[] qTable = null;

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => coeffs.DequantizeInPlace(qTable));
    }

    [Fact]
    public void DequantizeInPlace_WithZeroQuantizationValues_SetsAllToZero()
    {
        // Arrange
        double[] coeffs = new double[BLOCK_SIZE];
        double[] qTable = new double[BLOCK_SIZE];

        for (int i = 0; i < BLOCK_SIZE; i++)
        {
            coeffs[i] = i + 1;
            qTable[i] = 0.0;
        }

        // Act
        double[] result = coeffs.DequantizeInPlace(qTable);

        // Assert
        for (int i = 0; i < BLOCK_SIZE; i++)
        {
            Assert.Equal(0.0, result[i]);
        }
    }

    [Fact]
    public void DequantizeInPlace_WithOneQuantizationValue_MultipliesCorrectly()
    {
        // Arrange
        double[] coeffs = new double[BLOCK_SIZE];
        double[] qTable = new double[BLOCK_SIZE];
        const double testValue = 2.5;

        for (int i = 0; i < BLOCK_SIZE; i++)
        {
            coeffs[i] = i + 1;
            qTable[i] = testValue;
        }

        // Act
        double[] result = coeffs.DequantizeInPlace(qTable);

        // Assert
        for (int i = 0; i < BLOCK_SIZE; i++)
        {
            Assert.Equal((i + 1) * testValue, result[i], 10);
        }
    }

    [Fact]
    public void DequantizeInPlace_WithNegativeCoefficients_ReturnsCorrectValues()
    {
        // Arrange
        double[] coeffs = new double[BLOCK_SIZE];
        double[] qTable = new double[BLOCK_SIZE];

        for (int i = 0; i < BLOCK_SIZE; i++)
        {
            coeffs[i] = (i % 2 == 0) ? (i + 1) : -(i + 1); // Alternating positive and negative
            qTable[i] = 2.0;
        }

        // Act
        double[] result = coeffs.DequantizeInPlace(qTable);

        // Assert
        for (int i = 0; i < BLOCK_SIZE; i++)
        {
            double expected = ((i % 2 == 0) ? (i + 1) : -(i + 1)) * 2.0;
            Assert.Equal(expected, result[i], 10);
        }
    }

    [Fact]
    public void DequantizeInPlace_WithFractionalValues_ReturnsCorrectValues()
    {
        // Arrange
        double[] coeffs = new double[BLOCK_SIZE];
        double[] qTable = new double[BLOCK_SIZE];

        for (int i = 0; i < BLOCK_SIZE; i++)
        {
            coeffs[i] = (i + 1) * 0.5; // 0.5, 1.0, 1.5, ...
            qTable[i] = (i + 1) * 0.25; // 0.25, 0.5, 0.75, ...
        }

        // Calculate expected results manually
        double[] expected = new double[BLOCK_SIZE];
        for (int i = 0; i < BLOCK_SIZE; i++)
        {
            expected[i] = coeffs[i] * qTable[i];
        }

        // Act
        double[] result = coeffs.DequantizeInPlace(qTable);

        // Assert
        for (int i = 0; i < BLOCK_SIZE; i++)
        {
            Assert.Equal(expected[i], result[i], 10);
        }
    }

    [Fact]
    public void DequantizeInPlace_ModifiesOriginalArray()
    {
        // Arrange
        double[] coeffs = new double[BLOCK_SIZE];
        double[] qTable = new double[BLOCK_SIZE];
        double[] originalCoeffs = new double[BLOCK_SIZE];

        for (int i = 0; i < BLOCK_SIZE; i++)
        {
            coeffs[i] = i + 1;
            originalCoeffs[i] = i + 1;
            qTable[i] = 2.0;
        }

        // Act
        coeffs.DequantizeInPlace(qTable);

        // Assert - original array should be modified
        for (int i = 0; i < BLOCK_SIZE; i++)
        {
            Assert.NotEqual(originalCoeffs[i], coeffs[i]);
            Assert.Equal((i + 1) * 2.0, coeffs[i]);
        }
    }

    [Fact]
    public void DequantizeInPlace_HandlesVectorBoundariesCorrectly()
    {
        // This test specifically verifies that the vectorized loop and scalar remainder loop work together correctly
        // Arrange
        double[] coeffs = new double[BLOCK_SIZE];
        double[] qTable = new double[BLOCK_SIZE];

        // Use values that will test the boundary between vector and scalar processing
        for (int i = 0; i < BLOCK_SIZE; i++)
        {
            coeffs[i] = 1.0;
            qTable[i] = 3.0;
        }

        // Act
        double[] result = coeffs.DequantizeInPlace(qTable);

        // Assert - all values should be 3.0
        for (int i = 0; i < BLOCK_SIZE; i++)
        {
            Assert.Equal(3.0, result[i]);
        }
    }

    [Fact]
    public void DequantizeInPlace_ReturnsSameArrayReference()
    {
        // Arrange
        double[] coeffs = new double[BLOCK_SIZE];
        double[] qTable = new double[BLOCK_SIZE];

        for (int i = 0; i < BLOCK_SIZE; i++)
        {
            coeffs[i] = 1.0;
            qTable[i] = 1.0;
        }

        // Act
        double[] result = coeffs.DequantizeInPlace(qTable);

        // Assert
        Assert.Same(coeffs, result);
    }
}