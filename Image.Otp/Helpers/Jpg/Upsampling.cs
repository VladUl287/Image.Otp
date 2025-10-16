using System.Runtime.CompilerServices;

namespace Image.Otp.Core.Helpers.Jpg;

public static class Upsampling
{
    public static byte[] UpsampleInPlace(this byte[] output, Span<double> block, int width, int height, int scaleX, int scaleY, int blockStartX, int blockStartY)
    {
        const int BLOCK_SIZE = 8;

        Span<byte> pixels = stackalloc byte[BLOCK_SIZE * BLOCK_SIZE];
        for (var i = 0; i < BLOCK_SIZE * BLOCK_SIZE; i++)
            pixels[i] = ConvertSampleToByte(block[i]);

        for (var sy = 0; sy < BLOCK_SIZE; sy++)
        {
            var pixelRow = pixels.Slice(sy * BLOCK_SIZE, BLOCK_SIZE);
            var baseY = blockStartY + sy * scaleY;

            var endY = Math.Min(baseY + scaleY, height);
            var startY = Math.Max(baseY, 0);

            for (var y = startY; y < endY; y++)
            {
                var rowOffset = y * width;
                for (var sx = 0; sx < BLOCK_SIZE; sx++)
                {
                    var pixel = pixelRow[sx];
                    var baseX = blockStartX + sx * scaleX;

                    var endX = Math.Min(baseX + scaleX, width);
                    var startX = Math.Max(baseX, 0);

                    for (var x = startX; x < endX; x++)
                        output[rowOffset + x] = pixel;
                }
            }
        }

        return output;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static byte ConvertSampleToByte(double sample)
    {
        int value = (int)(sample + 128.5);
        uint clamped = (uint)Math.Max(0, Math.Min(value, 255));
        return (byte)clamped;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private unsafe static void FillScaledBlock(byte pixelValue, int baseX, int baseY, int scaleX, int scaleY, int width, int height, byte[] output)
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
