using Image.Otp.Primitives;

namespace Image.Otp.Extensions;

public static class SaveExtensions
{
    public static unsafe void SaveAsBmp(this ImageOtp<Rgba32> image, string path)
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

    public static unsafe void SaveAsBmp(this ImageOtp<Rgb24> image, string path)
    {
        int rowSize = image.Width * 3;
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
        bw.Write((short)24); // Bits per pixel (RGBA)
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
            }
            for (int p = 0; p < padding; p++) bw.Write((byte)0);
        }
    }
}
