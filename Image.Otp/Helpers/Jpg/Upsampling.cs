using System.Runtime.CompilerServices;

namespace Image.Otp.Core.Helpers.Jpg;

public static class Upsampling
{
    public static byte[] UpsampleInPlace(this byte[] output, Span<double> block, int maxH, int maxV, int width, int height, int my, int mx,
    int scaleX, int scaleY, int by, int bx)
    {
        const int BlockSize = 8;

        var blockStartX = mx * maxH * BlockSize + bx * BlockSize * scaleX;
        var blockStartY = my * maxV * BlockSize + by * BlockSize * scaleY;

        return output.UpsamplingScalarFallback(block, blockStartX, blockStartY, width, height, scaleX, scaleY);
    }

    private static byte[] UpsamplingScalarFallback(this byte[] output, Span<double> block, int blockStartX, int blockStartY, int width, int height, int scaleX, int scaleY)
    {
        const int BLOCK_SIZE = 8;

        for (int sy = 0; sy < BLOCK_SIZE; sy++)
        {
            for (int sx = 0; sx < BLOCK_SIZE; sx++)
            {
                var pixel = ConvertSampleToByte(block[sy * BLOCK_SIZE + sx]);

                var baseX = blockStartX + sx * scaleX;
                var baseY = blockStartY + sy * scaleY;

                FillScaledBlock(pixel, baseX, baseY, scaleX, scaleY, width, height, output);
            }
        }

        return output;
    }

    public static byte[] UpsampleInPlace(this byte[] output, double[] block, int maxH, int maxV, int width, int height, int my, int mx,
        int scaleX, int scaleY, int by, int bx)
    {
        const int BlockSize = 8;

        var blockStartX = mx * maxH * BlockSize + bx * BlockSize * scaleX;
        var blockStartY = my * maxV * BlockSize + by * BlockSize * scaleY;

        return output.UpsamplingScalarFallback(block, blockStartX, blockStartY, width, height, scaleX, scaleY);
    }

    private static byte[] UpsamplingScalarFallback(this byte[] output, double[] block, int blockStartX, int blockStartY, int width, int height, int scaleX, int scaleY)
    {
        const int BLOCK_SIZE = 8;

        for (int sy = 0; sy < BLOCK_SIZE; sy++)
        {
            for (int sx = 0; sx < BLOCK_SIZE; sx++)
            {
                var pixel = ConvertSampleToByte(block[sy * BLOCK_SIZE + sx]);

                var baseX = blockStartX + sx * scaleX;
                var baseY = blockStartY + sy * scaleY;

                FillScaledBlock(pixel, baseX, baseY, scaleX, scaleY, width, height, output);
            }
        }

        return output;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static byte ConvertSampleToByte(double sample)
    {
        const double OFFSET = 128.0;
        var value = (int)Math.Round(sample + OFFSET);
        return (byte)Math.Clamp(value, 0, 255);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void FillScaledBlock(byte pixelValue, int baseX, int baseY,
        int scaleX, int scaleY, int width, int height, byte[] output)
    {
        var endY = Math.Min(baseY + scaleY, height);
        var endX = Math.Min(baseX + scaleX, width);

        var startY = Math.Max(baseY, 0);
        var startX = Math.Max(baseX, 0);

        for (var y = startY; y < endY; y++)
        {
            var rowOffset = y * width;
            for (var x = startX; x < endX; x++)
            {
                output[rowOffset + x] = pixelValue;
            }
        }
    }
}
