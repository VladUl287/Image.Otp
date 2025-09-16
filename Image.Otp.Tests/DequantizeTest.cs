using Image.Otp.Models.Jpeg;

namespace Image.Otp.Tests;

public class DequantizeInverseZigZagIdctTests
{
    [Fact]
    public void DequantizeInverseZigZagIdct_SingleDcProducesUniformBlock()
    {
        // Arrange
        short[] quantizedCoeffs = new short[64];
        quantizedCoeffs[0] = 10; // Only DC = 10, all AC=0

        ushort[] quantTable = new ushort[64];
        for (int i = 0; i < 64; i++) quantTable[i] = 1; // simple QTable all ones

        // Act
        byte[] block = JpegIdct.DequantizeInverseZigZagIdct(quantizedCoeffs, quantTable);

        // Assert
        Assert.Equal(64, block.Length);

        // All pixels should be equal because AC=0
        byte first = block[0];
        foreach (byte b in block)
        {
            Assert.Equal(first, b);
        }

        // The value should be around 128 + something small
        Assert.InRange(first, (byte)128, (byte)130);
    }

    [Fact]
    public void DequantizeInverseZigZagIdct_ZeroBlockProduces128()
    {
        // Arrange
        short[] quantizedCoeffs = new short[64]; // all zeros
        ushort[] quantTable = new ushort[64];
        for (int i = 0; i < 64; i++) quantTable[i] = 1;

        // Act
        byte[] block = JpegIdct.DequantizeInverseZigZagIdct(quantizedCoeffs, quantTable);

        // Assert
        Assert.Equal(64, block.Length);
        foreach (byte b in block)
        {
            Assert.Equal(128, b); // DC=0 -> after level shift = 128
        }
    }

    [Fact]
    public void DequantizeInverseZigZagIdct_AllOnesCoeffs()
    {
        // Arrange
        short[] quantizedCoeffs = new short[64];
        for (int i = 0; i < 64; i++) quantizedCoeffs[i] = 1; // DC=1, AC=1
        ushort[] quantTable = new ushort[64];
        for (int i = 0; i < 64; i++) quantTable[i] = 1;

        // Act
        byte[] block = JpegIdct.DequantizeInverseZigZagIdct(quantizedCoeffs, quantTable);

        // Assert
        Assert.Equal(64, block.Length);

        // Clamp check: all values in [0,255]
        foreach (byte b in block) Assert.InRange(b, (byte)0, (byte)255);

        // Optional: check mean is approximately correct
        double mean = block.Average(b => (double)b);
        Assert.InRange(mean, 128.0, 140.0); // expected slightly above 128
    }
}
