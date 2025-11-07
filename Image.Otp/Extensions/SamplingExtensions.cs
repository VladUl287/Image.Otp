using System.Numerics;
using System.Runtime.CompilerServices;

namespace Image.Otp.Core.Extensions;

public static class Upsampling
{
    public static Span<float> UpsampleInPlace(this Span<float> block, byte[] output, int width, int height, int scaleX, int scaleY, int blockStartX, int blockStartY)
    {
        const int BLOCK_SIZE = 8;

        //var startY = Math.Max(blockStartY, 0);
        //var endY = Math.Min(blockStartY + BLOCK_SIZE * scaleY, height);
        //var startX = Math.Max(blockStartX, 0);
        //var endX = Math.Min(blockStartX + BLOCK_SIZE * scaleX, width);

        //for (var y = startY; y < endY; y++)
        //{
        //    var rowOffset = y * width;

        //    var sourceY = (y - blockStartY) / scaleY;
        //    if (sourceY < 0 || sourceY >= BLOCK_SIZE) continue;

        //    var srcRow = block.Slice(sourceY * BLOCK_SIZE, BLOCK_SIZE);

        //    for (var x = startX; x < endX; x++)
        //    {
        //        var sourceX = (x - blockStartX) / scaleX;
        //        if (sourceX < 0 || sourceX >= BLOCK_SIZE) continue;

        //        output[rowOffset + x] = (byte)ClampToByte(srcRow[sourceX]);
        //    }
        //}

        for (var y = 0; y < BLOCK_SIZE; y++)
        {
            for (var x = 0; x < BLOCK_SIZE; x++)
            {
                var sampleByte = (byte)ClampToByte(block[y * 8 + x]);

                for (var uy = 0; uy < scaleY; uy++)
                {
                    var outY = blockStartY + y * scaleY + uy;
                    if (outY < 0 || outY >= height) continue;

                    for (var ux = 0; ux < scaleX; ux++)
                    {
                        var outX = blockStartX + x * scaleX + ux;
                        if (outX < 0 || outX >= width) continue;

                        var dstIndex = outY * width + outX;
                        output[dstIndex] = sampleByte;
                    }
                }
            }
        }

        return block;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static float ClampToByte(float sample)
    {
        var value = sample + 128.0f;
        return Math.Max(0f, Math.Min(value, 255f));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static byte ConvertSampleToByte(float sample)
    {
        int value = (int)(sample + 128.5);
        uint clamped = (uint)Math.Max(0, Math.Min(value, 255));
        return (byte)clamped;
    }
}
