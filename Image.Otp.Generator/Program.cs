using System.Drawing;
using System.Drawing.Imaging;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Formats.Jpeg;

public sealed class Program
{
    static void Main()
    {
        Directory.CreateDirectory("output");

        //using var img = SixLabors.ImageSharp.Image.Load<Rgb24>(@"output\fine_text_no_subsampling_bitmap.png");
        //var encoder = new JpegEncoder { Quality = 100, ColorType = JpegEncodingColor.YCbCrRatio444 };
        //img.Save(@"output\fine_text_no_subsampling_bitmap.jpg", encoder);

        //SaveBoth("checkerboard_8.png", CreateCheckerboard(256, 256, 8));
        //SaveBoth("checkerboard_16.png", CreateCheckerboard(256, 256, 16));
        //SaveBoth("checkerboard_32.png", CreateCheckerboard(256, 256, 32));
        //SaveBoth("gradient_stripes.png", CreateGradientWithStripes(256, 256));
        //SaveBoth("fine_text.png", CreateFineTextImage(1024, 1024));
        //SaveBoth("primaries_edges.png", CreatePrimaryEdges(256, 256));
        //SaveBoth("onepx_lines.png", CreateOnePxLines(256, 256));
    }

    // Helper that saves same image via Bitmap and ImageSharp
    static void SaveBoth(string baseName, System.Drawing.Bitmap bmp)
    {
        string nameNoExt = Path.GetFileNameWithoutExtension(baseName);

        // Save PNG via Bitmap
        string bmpPng = Path.Combine("output", nameNoExt + "_bitmap.png");
        bmp.Save(bmpPng, ImageFormat.Png);

        // Save JPEG via Bitmap (baseline)
        string bmpJpeg = Path.Combine("output", nameNoExt + "_bitmap.jpg");
        var jpgEncoder = GetEncoder(ImageFormat.Jpeg);
        var encParams = new EncoderParameters(1);
        encParams.Param[0] = new EncoderParameter(Encoder.Quality, 100L);
        bmp.Save(bmpJpeg, jpgEncoder, encParams);

        // Convert Bitmap -> ImageSharp and save with ImageSharp
        //using (var ms = new MemoryStream())
        //{
        //    bmp.Save(ms, ImageFormat.Png);
        //    ms.Position = 0;
        //    using (var img = SixLabors.ImageSharp.Image.Load<Rgba32>(ms))
        //    {
        //        string isharpPng = Path.Combine("output", nameNoExt + "_imagesharp.png");
        //        img.Save(isharpPng);

        //        string isharpJpeg = Path.Combine("output", nameNoExt + "_imagesharp_q90.jpg");
        //        var jpegOpts = new JpegEncoder { Quality = 100 };
        //        img.SaveAsJpeg(isharpJpeg, jpegOpts);

        //        // Also save a progressive JPEG variant for the progressive_test image
        //        //if (nameNoExt.Contains("progressive_test"))
        //        //{
        //        //    string isharpProg = Path.Combine("output", nameNoExt + "_imagesharp_progressive.jpg");
        //        //    var jpegProg = new JpegEncoder { Quality = 100 };
        //        //    img.Save(isharpProg, jpegProg);
        //        //}
        //    }
        //}

        Console.WriteLine("Saved: " + nameNoExt);
    }

    static ImageCodecInfo GetEncoder(ImageFormat format)
    {
        var codecs = ImageCodecInfo.GetImageDecoders();
        foreach (var codec in codecs)
            if (codec.FormatID == format.Guid) return codec;
        return null;
    }

    // Pattern generators using System.Drawing.Bitmap

    static Bitmap CreateCheckerboard(int width, int height, int blockSize)
    {
        var bmp = new Bitmap(width, height, System.Drawing.Imaging.PixelFormat.Format24bppRgb);
        using (var g = Graphics.FromImage(bmp))
        {
            g.Clear(System.Drawing.Color.White);
            bool toggleRow = false;
            for (int y = 0; y < height; y += blockSize)
            {
                bool toggle = toggleRow;
                for (int x = 0; x < width; x += blockSize)
                {
                    var rect = new System.Drawing.Rectangle(x, y, blockSize, blockSize);
                    g.FillRectangle(toggle ? Brushes.Black : Brushes.LightGray, rect);
                    toggle = !toggle;
                }
                toggleRow = !toggleRow;
            }
        }
        return bmp;
    }

    static Bitmap CreateGradientWithStripes(int width, int height)
    {
        var bmp = new Bitmap(width, height, System.Drawing.Imaging.PixelFormat.Format24bppRgb);
        for (int y = 0; y < height; y++)
        {
            // horizontal gradient from black to white
            for (int x = 0; x < width; x++)
            {
                int g = (int)(255.0 * x / (width - 1));
                // add thin high-frequency vertical stripes every 8px
                if ((x % 8) < 1) g = Math.Clamp(g + 80, 0, 255);
                bmp.SetPixel(x, y, System.Drawing.Color.FromArgb(g, g, g));
            }
        }
        return bmp;
    }

    static Bitmap CreateFineTextImage(int width, int height)
    {
        var bmp = new Bitmap(width, height, System.Drawing.Imaging.PixelFormat.Format24bppRgb);
        using (var g = Graphics.FromImage(bmp))
        {
            g.Clear(System.Drawing.Color.White);
            // Draw multiple sizes of text, rotated slightly
            var fonts = new[] { 8f, 10f, 12f, 16f, 24f };
            int y = 10;
            foreach (var fs in fonts)
            {
                using (var font = new Font("Consolas", fs, System.Drawing.FontStyle.Regular, GraphicsUnit.Pixel))
                {
                    g.TranslateTransform(5, y);
                    g.RotateTransform(-2); // slight rotation
                    g.DrawString("The quick brown fox jumps over 12 lazy dogs 0123456789", font, Brushes.Black, 0, 0);
                    g.ResetTransform();
                }
                y += (int)(fs * 2.5);
            }

            // add 1px thin lines for additional detail
            for (int i = 0; i < 50; i++)
            {
                g.DrawLine(Pens.Black, 600, 10 + i * 8, width - 10, 10 + i * 8);
            }
        }
        return bmp;
    }

    static Bitmap CreatePrimaryEdges(int width, int height)
    {
        var bmp = new Bitmap(width, height, System.Drawing.Imaging.PixelFormat.Format24bppRgb);
        using (var g = Graphics.FromImage(bmp))
        {
            int third = width / 3;
            g.FillRectangle(Brushes.Red, 0, 0, third, height);
            g.FillRectangle(Brushes.Green, third, 0, third, height);
            g.FillRectangle(Brushes.Blue, 2 * third, 0, width - 2 * third, height);

            // Add vertical sharp edges between primaries with 1px boundary lines
            g.FillRectangle(Brushes.White, third - 1, 0, 2, height);
            g.FillRectangle(Brushes.White, 2 * third - 1, 0, 2, height);
        }
        return bmp;
    }

    static Bitmap CreateOnePxLines(int width, int height)
    {
        var bmp = new Bitmap(width, height, System.Drawing.Imaging.PixelFormat.Format24bppRgb);
        using (var g = Graphics.FromImage(bmp))
        {
            g.Clear(System.Drawing.Color.White);
            // Horizontal 1px alternating color lines
            for (int y = 0; y < height; y++)
            {
                System.Drawing.Color c = (y % 6) switch
                {
                    0 => System.Drawing.Color.Red,
                    1 => System.Drawing.Color.Green,
                    2 => System.Drawing.Color.Blue,
                    3 => System.Drawing.Color.Magenta,
                    4 => System.Drawing.Color.Cyan,
                    _ => System.Drawing.Color.Yellow
                };
                g.DrawLine(new Pen(c, 1), 0, y, width, y);
            }

            // Vertical 1px lines of alternating colors every 2px
            for (int x = 0; x < width; x += 2)
            {
                System.Drawing.Color c = (x % 6) switch
                {
                    0 => System.Drawing.Color.Red,
                    2 => System.Drawing.Color.Green,
                    4 => System.Drawing.Color.Blue,
                    _ => System.Drawing.Color.Black
                };
                g.DrawLine(new Pen(c, 1), x, 0, x, height);
            }
        }
        return bmp;
    }
}