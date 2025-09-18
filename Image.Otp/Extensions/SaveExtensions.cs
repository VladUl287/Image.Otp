using Image.Otp.Primitives;
using System.Drawing.Imaging;
using System.Drawing;

namespace Image.Otp.Extensions;

public static class SaveExtensions
{
    public static unsafe void SaveAsBmp(this ImageNative<Rgba32> image, string path)
    {
        int rowSize = image.Width * 4;
        int padding = (4 - (rowSize % 4)) % 4;
        int fileSize = 54 + (rowSize + padding) * image.Height;

        using var fs = new FileStream(path, FileMode.Create);
        using var bw = new BinaryWriter(fs);

        // BMP Header
        bw.Write(new[] { 'B', 'M' }); // Signature
        bw.Write(fileSize); // File size
        bw.Write(0); // Reserved
        bw.Write(54); // Pixel data offset

        // DIB Header
        bw.Write(40); // Header size
        bw.Write(image.Width);
        bw.Write(image.Height);
        bw.Write((short)1); // Planes
        bw.Write((short)32); // Bits per pixel (RGBA)
        bw.Write(0); // Compression (none)
        bw.Write(0); // Image size (can be 0 for uncompressed)
        bw.Write(0); // X pixels per meter
        bw.Write(0); // Y pixels per meter
        bw.Write(0); // Colors in palette
        bw.Write(0); // Important colors

        // Pixel data (bottom-up)
        for (int y = image.Height - 1; y >= 0; y--)
        {
            var row = image.Pixels.Slice(y * image.Width, image.Width);
            foreach (ref var pixel in row)
            {
                bw.Write(pixel.B); // BMP is BGR
                bw.Write(pixel.G);
                bw.Write(pixel.R);
                bw.Write(pixel.A);
            }
            for (int p = 0; p < padding; p++) bw.Write((byte)0);
        }
    }

    public static unsafe void SaveAsBmp(this Image<Rgba32> image, string path)
    {
        int rowSize = image.Width * 4;
        int padding = (4 - (rowSize % 4)) % 4;
        int fileSize = 54 + (rowSize + padding) * image.Height;

        using var fs = new FileStream(path, FileMode.Create);
        using var bw = new BinaryWriter(fs);

        // BMP Header
        bw.Write(new[] { 'B', 'M' }); // Signature
        bw.Write(fileSize); // File size
        bw.Write(0); // Reserved
        bw.Write(54); // Pixel data offset

        // DIB Header
        bw.Write(40); // Header size
        bw.Write(image.Width);
        bw.Write(image.Height);
        bw.Write((short)1); // Planes
        bw.Write((short)32); // Bits per pixel (RGBA)
        bw.Write(0); // Compression (none)
        bw.Write(0); // Image size (can be 0 for uncompressed)
        bw.Write(0); // X pixels per meter
        bw.Write(0); // Y pixels per meter
        bw.Write(0); // Colors in palette
        bw.Write(0); // Important colors

        // Pixel data (bottom-up)
        for (int y = image.Height - 1; y >= 0; y--)
        {
            var row = image.Pixels.Slice(y * image.Width, image.Width);
            foreach (ref var pixel in row)
            {
                bw.Write(pixel.B); // BMP is BGR
                bw.Write(pixel.G);
                bw.Write(pixel.R);
                bw.Write(pixel.A);
            }
            for (int p = 0; p < padding; p++) bw.Write((byte)0);
        }
    }

    public static void SaveRgba32ToBmp(byte[] rgba, int width, int height, string filePath)
    {
        // Ensure rgba.Length == width * height * 4
        if (rgba.Length != width * height * 4)
            throw new ArgumentException("RGBA array size does not match dimensions.");

        using var bmp = new Bitmap(width, height, PixelFormat.Format32bppArgb);
        var rect = new Rectangle(0, 0, width, height);
        var bmpData = bmp.LockBits(rect, ImageLockMode.WriteOnly, bmp.PixelFormat);

        // Copy all pixels directly
        System.Runtime.InteropServices.Marshal.Copy(rgba, 0, bmpData.Scan0, rgba.Length);

        bmp.UnlockBits(bmpData);
        bmp.Save(filePath, ImageFormat.Bmp);
    }
}
