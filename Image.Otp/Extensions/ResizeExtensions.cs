using Image.Otp.Primitives;
using System.Runtime.Intrinsics.X86;
using System.Runtime.Intrinsics;

namespace Image.Otp.Extensions;

public static class ResizeExtensions
{
    public static Image<T> ResizeNearestNeighbor<T>(this Image<T> source, int newWidth, int newHeight) where T : unmanaged
    {
        var dest = new Image<T>(newWidth, newHeight);
        float scaleX = (float)source.Width / newWidth;
        float scaleY = (float)source.Height / newHeight;

        for (int y = 0; y < newHeight; y++)
        {
            int srcY = (int)(y * scaleY);
            for (int x = 0; x < newWidth; x++)
            {
                int srcX = (int)(x * scaleX);
                dest.GetPixel(x, y) = source.GetPixel(srcX, srcY);
            }
        }
        return dest;
    }

    public static unsafe Image<Rgba32> ResizeNearestNeighborSimd(Image<Rgba32> source, int newWidth, int newHeight)
    {
        if (!Avx2.IsSupported) throw new Exception();

        var dest = new Image<Rgba32>(newWidth, newHeight);

        float scaleX = (float)source.Width / newWidth;
        float scaleY = (float)source.Height / newHeight;

        fixed (Rgba32* srcPtr = source.Pixels)
        fixed (Rgba32* dstPtr = dest.Pixels)
        {
            for (int y = 0; y < newHeight; y++)
            {
                int srcY = (int)(y * scaleY);
                Rgba32* srcRow = srcPtr + (srcY * source.Width);
                Rgba32* dstRow = dstPtr + (y * newWidth);

                int x = 0;

                // Precompute constants outside the loop
                var scaleXVec = Vector256.Create(scaleX);
                var xOffsets = Vector256.Create(0f, 1f, 2f, 3f, 4f, 5f, 6f, 7f);
                var xBase = Vector256.Create(0f);

                for (; x <= newWidth - Vector256<int>.Count; x += Vector256<int>.Count)
                {
                    // Calculate all 8 indices at once
                    xBase = Vector256.Create((float)x);
                    var xPos = Avx.Add(xBase, xOffsets);
                    var srcX = Avx.Multiply(xPos, scaleXVec);
                    var srcXi = Avx.ConvertToVector256Int32(srcX);

                    // Gather pixels
                    var size = (byte)sizeof(Rgba32);
                    var pixels = Avx2.GatherVector256((int*)srcRow, srcXi, size);

                    // Store
                    Avx.Store((float*)(dstRow + x), pixels.AsSingle());
                }

                // Handle remainder
                for (; x < newWidth; x++)
                {
                    int srcX = (int)(x * scaleX);
                    dstRow[x] = srcRow[srcX];
                }
            }
        }

        return dest;
    }
}
