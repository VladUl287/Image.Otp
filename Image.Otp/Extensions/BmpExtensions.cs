using Image.Otp.Core.Primitives;

namespace Image.Otp.Core.Extensions;

public static class BmpExtensions
{
    public unsafe static Image<T> LoadBmp<T>(this Stream stream) where T : unmanaged
    {
        using var br = new BinaryReader(stream);

        // BMP Header (54 bytes)
        br.ReadBytes(14); // Skip file header
        int headerSize = BitConverter.ToInt32(br.ReadBytes(4));
        int width = BitConverter.ToInt32(br.ReadBytes(4));
        int height = BitConverter.ToInt32(br.ReadBytes(4));
        br.ReadBytes(2);  // Skip planes
        int bitsPerPixel = BitConverter.ToInt16(br.ReadBytes(2));
        br.ReadBytes(headerSize - 24); // Skip remaining header

        if (bitsPerPixel != 24 && bitsPerPixel != 32)
            throw new NotSupportedException("Only 24/32bpp BMP supported");

        var image = new Image<T>(width, Math.Abs(height));
        bool topDown = height < 0;
        height = Math.Abs(height);

        // Pixel data (BGR/BGRA format)
        int bytesPerPixel = bitsPerPixel / 8;
        int rowSize = (width * bytesPerPixel + 3) & ~3; // 4-byte aligned

        fixed (T* dstPtr = &image.Pixels[0])
        {
            for (int y = 0; y < height; y++)
            {
                int dstY = topDown ? y : height - 1 - y;
                byte[] rowData = br.ReadBytes(rowSize);

                fixed (byte* srcPtr = rowData)
                {
                    for (int x = 0; x < width; x++)
                    {
                        int srcPos = x * bytesPerPixel;
                        int dstPos = dstY * width + x;

                        if (typeof(T) == typeof(Rgba32))
                        {
                            byte a = bytesPerPixel == 4 ? srcPtr[srcPos + 3] : (byte)255;
                            ((Rgba32*)dstPtr)[dstPos] = new Rgba32(
                                srcPtr[srcPos + 2], // R
                                srcPtr[srcPos + 1], // G
                                srcPtr[srcPos + 0], // B
                                a                  // A
                            );
                        }
                    }
                }
            }
        }

        return image;
    }
}
