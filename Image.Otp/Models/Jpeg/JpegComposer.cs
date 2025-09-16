using Image.Otp.Primitives;
using System.Runtime.CompilerServices;

public static class JpegComposer
{
    // Integer BT.601 YCbCr -> RGB (JPEG full-range). Cb/Cr are -128..127 here.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int Clamp(int v) => (v < 0) ? 0 : (v > 255 ? 255 : v);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void YCbCrToRgb(byte y, int cb, int cr, out byte r, out byte g, out byte b)
    {
        // r = y + 1.402 * cr
        // g = y - 0.344136 * cb - 0.714136 * cr
        // b = y + 1.772 * cb
        // Fixed-point (>>16) constants to match common decoders:
        int Y = y;
        int R = Y + ((91881 * cr) >> 16);
        int G = Y - ((22554 * cb + 46802 * cr) >> 16);
        int B = Y + ((116130 * cb) >> 16);

        r = (byte)Clamp(R);
        g = (byte)Clamp(G);
        b = (byte)Clamp(B);
    }

    /// <summary>
    /// Write one MCU for 4:2:0 (Y: h=2,v=2; Cb/Cr: h=1,v=1).
    /// Y00,Y10 are the top 8x8s; Y01,Y11 are the bottom 8x8s. Cb/Cr are 8x8 each.
    /// Expands Cb/Cr by 2x2 per chroma sample and converts immediately to RGBA.
    /// </summary>
    public static void WriteMcu420(
        ReadOnlySpan<byte> Y00, ReadOnlySpan<byte> Y10,
        ReadOnlySpan<byte> Y01, ReadOnlySpan<byte> Y11,
        ReadOnlySpan<byte> Cb, ReadOnlySpan<byte> Cr,
        int mcuX, int mcuY, int width, int height,
        Rgba32[] dst // RGBA (R,G,B,A) — set A=255
    )
    {
        // MCU covers 16x16 output pixels
        int baseX = mcuX * 16;
        int baseY = mcuY * 16;

        // Clip to image edges (handles widths/heights not divisible by 16)
        int mcuW = Math.Min(16, width - baseX);
        int mcuH = Math.Min(16, height - baseY);

        for (int yy = 0; yy < mcuH; yy++)
        {
            int outY = baseY + yy;
            int rowOff = outY * width;

            // Which Y block (top/bottom, left/right)?
            bool top = yy < 8;
            int yRow = (yy & 7) * 8; // 0..7 mapped to row within the 8x8

            for (int xx = 0; xx < mcuW; xx++)
            {
                int outX = baseX + xx;

                bool left = xx < 8;
                int yCol = (xx & 7); // 0..7 col within 8x8

                byte yVal = (top, left) switch
                {
                    (true, true) => Y00[yRow + yCol],
                    (true, false) => Y10[yRow + (yCol)],     // right block, same row
                    (false, true) => Y01[yRow + yCol],       // bottom-left
                    _ => Y11[yRow + (yCol)],     // bottom-right
                };

                // 2x2 upsample from Cb/Cr: pick chroma at (xx>>1, yy>>1) in 8x8
                int cIndex = ((yy >> 1) * 8) + (xx >> 1);
                int cb = Cb[cIndex] - 128;
                int cr = Cr[cIndex] - 128;

                YCbCrToRgb(yVal, cb, cr, out byte r, out byte g, out byte b);

                dst[rowOff + outX] = new Rgba32(r, g, b, 255);
            }
        }
    }

    /// <summary>
    /// High-level loop that iterates MCUs and calls your entropy/IDCT to fill the 6 blocks.
    /// You plug your own DecodeMcu420() that returns 4 Y blocks + Cb + Cr (all 8x8 = 64 bytes each).
    /// </summary>
    public static void ComposeImage420(
        int width, int height,
        Func<(byte[] Y00, byte[] Y10, byte[] Y01, byte[] Y11, byte[] Cb, byte[] Cr)> DecodeMcu420,
        Rgba32[] dst // output buffer length = width*height
    )
    {
        int mcuCols = (width + 15) / 16;
        int mcuRows = (height + 15) / 16;

        for (int my = 0; my < mcuRows; my++)
        {
            for (int mx = 0; mx < mcuCols; mx++)
            {
                var (Y00, Y10, Y01, Y11, Cb, Cr) = DecodeMcu420();

                // Expect each to be length 64 (8x8). If not, your IDCT/output staging is off.
                if (Y00.Length != 64 || Y10.Length != 64 || Y01.Length != 64 ||
                    Y11.Length != 64 || Cb.Length != 64 || Cr.Length != 64)
                    throw new InvalidOperationException("Block size must be 8x8 (64 samples).");

                WriteMcu420(Y00, Y10, Y01, Y11, Cb, Cr, mx, my, width, height, dst);
            }
        }
    }
}