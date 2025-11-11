using System.Runtime.CompilerServices;

namespace Image.Otp.Core.Extensions;

public static class Upsampling
{
    public static void Upsample(Span<float> block, Span<float> output, int width, int height, int scaleX, int scaleY, int blockStartX, int blockStartY)
    {
        const int BLOCK_SIZE = 8;

        var startX = Math.Max(blockStartX, 0);
        var startY = Math.Max(blockStartY, 0);

        if (scaleX == 1 && scaleY == 1)
        {
            var dstStart = startY * width + startX;

            var colsToCopy = BLOCK_SIZE - Math.Max(0, startX + BLOCK_SIZE - width);
            if (colsToCopy <= 0) return;

            CopyTo1x1Scale(block, output[dstStart..], colsToCopy, width);
        }

        var endY = Math.Min(blockStartY + BLOCK_SIZE * scaleY, height);
        var endX = Math.Min(blockStartX + BLOCK_SIZE * scaleX, width);

        for (var y = startY; y < endY; y++)
        {
            var rowOffset = y * width;

            var sourceY = (y - blockStartY) / scaleY;
            if (sourceY < 0 || sourceY >= BLOCK_SIZE) continue;

            var srcRow = block.Slice(sourceY * BLOCK_SIZE, BLOCK_SIZE);

            for (var x = startX; x < endX; x++)
            {
                var sourceX = (x - blockStartX) / scaleX;
                if (sourceX < 0 || sourceX >= BLOCK_SIZE) continue;

                output[rowOffset + x] = srcRow[sourceX];
            }
        }
    }

    private static void CopyTo1x1Scale(Span<float> src, Span<float> dst, int blockWidth, int width)
    {
        const int BLOCK_SIZE = 8;

        CopyRowImpl(src, dst, blockWidth, width, 0);
        CopyRowImpl(src, dst, blockWidth, width, 1);
        CopyRowImpl(src, dst, blockWidth, width, 2);
        CopyRowImpl(src, dst, blockWidth, width, 3);
        CopyRowImpl(src, dst, blockWidth, width, 4);
        CopyRowImpl(src, dst, blockWidth, width, 5);
        CopyRowImpl(src, dst, blockWidth, width, 6);
        CopyRowImpl(src, dst, blockWidth, width, 7);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static void CopyRowImpl(Span<float> src, Span<float> dst, int colsToCopy, int width, int row)
        {
            var srcRow = src.Slice(row * BLOCK_SIZE, colsToCopy);
            var dstRow = dst.Slice(row * width, colsToCopy);
            srcRow.CopyTo(dstRow);
        }
    }
}
