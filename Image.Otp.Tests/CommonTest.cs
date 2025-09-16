using Xunit;
using System;
using System.Collections.Generic;
using System.Linq;
using Image.Otp.Models.Jpeg;

namespace Image.Otp.Tests;

public class DecodeScanToBlocks_FinalTests
{
    // canonical zig-zag (same as in your code)
    private static readonly int[] ZigZag = new int[] {
         0, 1, 5, 6,14,15,27,28,
         2, 4, 7,13,16,26,29,42,
         3, 8,12,17,25,30,41,43,
         9,11,18,24,31,40,44,53,
        10,19,23,32,39,45,52,54,
        20,22,33,38,46,51,55,60,
        21,34,37,47,50,56,59,61,
        35,36,48,49,57,58,62,63
    };

    // helper: manual inverse DCT (same math as your CompareBlock's InverseIDCTFromDequant)
    private static double[] InverseIDCTFromDequant(double[] dequantized /* natural order */)
    {
        double[] output = new double[64];
        for (int y = 0; y < 8; y++)
        {
            for (int x = 0; x < 8; x++)
            {
                double sum = 0.0;
                for (int u = 0; u < 8; u++)
                {
                    double cu = (u == 0) ? 1.0 / Math.Sqrt(2.0) : 1.0;
                    for (int v = 0; v < 8; v++)
                    {
                        double cv = (v == 0) ? 1.0 / Math.Sqrt(2.0) : 1.0;
                        double Fuv = dequantized[u * 8 + v];
                        double cos1 = Math.Cos((2 * x + 1) * u * Math.PI / 16.0);
                        double cos2 = Math.Cos((2 * y + 1) * v * Math.PI / 16.0);
                        sum += cu * cv * Fuv * cos1 * cos2;
                    }
                }
                output[y * 8 + x] = sum / 4.0; // raw spatial (no +128)
            }
        }
        return output;
    }

    // clamp helper
    private static byte ClampToByte(double v)
    {
        int i = (int)Math.Round(v);
        if (i < 0) i = 0;
        if (i > 255) i = 255;
        return (byte)i;
    }

    [Fact]
    public void Test5_DequantizeInverseZigZagIdct_MatchesManualPipeline()
    {
        // Prepare a synthetic quantized block in zigzag order (short[])
        // small, varied values so IDCT result is meaningful
        short[] quantZig = new short[64];
        quantZig[0] = 10;  // DC
        quantZig[1] = -2;  // AC
        quantZig[2] = 1;
        quantZig[5] = -1;
        // rest remain 0

        // quant table (natural order) - choose varied values so dequant multiplies
        ushort[] quantNatural = new ushort[64];
        for (int i = 0; i < 64; i++) quantNatural[i] = (ushort)(i % 8 + 1); // 1..8 repeating

        // Build dequantized natural array (manual)
        double[] dequantNatural = new double[64];
        for (int i = 0; i < 64; i++)
        {
            // quantZig index i corresponds to natural index nat = ZigZag[i]
            int nat = ZigZag[i];
            dequantNatural[nat] = quantZig[i] * quantNatural[nat];
        }

        // Manual IDCT -> spatial raw [-128..127], then +128 and clamp to byte
        double[] manualRaw = InverseIDCTFromDequant(dequantNatural);
        byte[] expectedPixels = new byte[64];
        for (int i = 0; i < 64; i++)
        {
            double v = manualRaw[i] + 128.0;
            expectedPixels[i] = ClampToByte(v);
        }

        // Call the user's implementation
        // Signature expected: JpegIdct.DequantizeInverseZigZagIdct(short[] coeffsZig, ushort[] quantTable)
        byte[] actualPixels;
        try
        {
            actualPixels = JpegIdct.DequantizeInverseZigZagIdct(quantZig, quantNatural);
        }
        catch (Exception ex)
        {
            Assert.False(true, "Failed to call JpegIdct.DequantizeInverseZigZagIdct: " + ex.Message);
            return;
        }

        Assert.NotNull(actualPixels);
        Assert.Equal(64, actualPixels.Length);

        // Compare element-wise
        for (int i = 0; i < 64; i++)
        {
            Assert.Equal(expectedPixels[i], actualPixels[i]);
        }
    }

    [Fact]
    public void Test6_DequantizeInverseZigZagIdct_DoesNotDoubleDequant()
    {
        // Use a block where quantized value and quant table produce easy-to-spot numbers
        short[] quantZig = new short[64];
        quantZig[0] = 2;   // DC = 2

        ushort[] quantNatural = new ushort[64];
        for (int i = 0; i < 64; i++) quantNatural[i] = 3; // every quant = 3

        // Manual dequant: natural[0] = quantZig[0] * quantNatural[0] = 6
        double[] dequantNatural = new double[64];
        dequantNatural[0] = 2 * 3;
        for (int i = 1; i < 64; i++) dequantNatural[i] = 0;

        // Manual IDCT -> compute raw spatial then +128 clamp
        double[] manualRaw = InverseIDCTFromDequant(dequantNatural);
        byte[] expectedPixels = new byte[64];
        for (int i = 0; i < 64; i++)
            expectedPixels[i] = ClampToByte(manualRaw[i] + 128.0);

        // Call user's implementation
        byte[] actualPixels;
        try
        {
            actualPixels = JpegIdct.DequantizeInverseZigZagIdct(quantZig, quantNatural);
        }
        catch (Exception ex)
        {
            Assert.False(true, "Failed to call JpegIdct.DequantizeInverseZigZagIdct: " + ex.Message);
            return;
        }

        Assert.Equal(expectedPixels, actualPixels); // will fail if double-multiplied (would yield different pixels)
    }

    [Fact]
    public void Test7_PostProcess_BlockPlacementAndYCbCrToRgb()
    {
        // Frame: single MCU, 3 components (Y, Cb, Cr), 1x1 sampling each, image 8x8
        var frame = new FrameInfo
        {
            Width = 8,
            Height = 8,
            Components = new List<ComponentInfo>
            {
                new ComponentInfo { Id = 1, SamplingFactor = 0x11, QuantizationTableId = 0 },
                new ComponentInfo { Id = 2, SamplingFactor = 0x11, QuantizationTableId = 0 },
                new ComponentInfo { Id = 3, SamplingFactor = 0x11, QuantizationTableId = 0 }
            }
        };

        // Scan info: components in Y, Cb, Cr order
        var scan = new ScanInfo
        {
            Components = new List<ScanComponent>
            {
                new ScanComponent { ComponentId = 1, DcHuffmanTableId = 0, AcHuffmanTableId = 0 },
                new ScanComponent { ComponentId = 2, DcHuffmanTableId = 0, AcHuffmanTableId = 0 },
                new ScanComponent { ComponentId = 3, DcHuffmanTableId = 0, AcHuffmanTableId = 0 }
            }
        };

        // Build frameCompById dictionary as expected by PostProcess
        var frameCompById = frame.Components.ToDictionary(c => c.Id);

        // Create synthetic pixel blocks (byte[64]) in natural spatial order (0..255)
        // We'll create simple constant blocks with different values for Y, Cb, Cr so RGB is easily predictable.
        byte[] blockY = Enumerable.Repeat((byte)100, 64).ToArray();   // Y = 100
        byte[] blockCb = Enumerable.Repeat((byte)120, 64).ToArray();  // Cb = 120
        byte[] blockCr = Enumerable.Repeat((byte)140, 64).ToArray();  // Cr = 140

        // Build allBlocks in scan order for a single MCU: [Yblock, Cbblock, Crblock]
        var allBlocks = new List<byte[]> { blockY, blockCb, blockCr };

        // Call PostProcess (expected signature as in your code)
        byte[] rgba;
        try
        {
            rgba = PostProcess(frame, scan, allBlocks, frameCompById);
        }
        catch (Exception ex)
        {
            Assert.False(true, "Failed to call PostProcess: " + ex.Message);
            return;
        }

        // Validate resulting image dimensions and a few pixel values
        Assert.NotNull(rgba);
        Assert.Equal(frame.Width * frame.Height * 4, rgba.Length);

        // Examine pixel (0,0)
        int pixelIndex = 0;
        int r = rgba[pixelIndex];
        int g = rgba[pixelIndex + 1];
        int b = rgba[pixelIndex + 2];

        // Compute expected from your YCbCr->RGB formula in PostProcess:
        // Cb' = Cb - 128, Cr' = Cr - 128

        float Yv = 100f;
        float CbV = 120f - 128f; // -8
        float CrV = 140f - 128f; // 12
        int expR = Clamp((int)(Yv + 1.40200f * CrV + 0.5f), 0, 255);
        int expG = Clamp((int)(Yv - 0.34414f * CbV - 0.71414f * CrV + 0.5f), 0, 255);
        int expB = Clamp((int)(Yv + 1.77200f * CbV + 0.5f), 0, 255);

        Assert.Equal(expR, r);
        Assert.Equal(expG, g);
        Assert.Equal(expB, b);

        // Also check another pixel (7,0) to ensure whole block placement is consistent
        int x = 7, y = 0;
        int idx = (y * frame.Width + x) * 4;
        Assert.Equal(expR, rgba[idx]);
        Assert.Equal(expG, rgba[idx + 1]);
        Assert.Equal(expB, rgba[idx + 2]);
    }

    // reuse the clamp function for Test7 expected values
    private static int Clamp(int v, int lo, int hi)
    {
        if (v < lo) return lo;
        if (v > hi) return hi;
        return v;
    }

    public static byte[] PostProcess(FrameInfo frameInfo, ScanInfo scanInfo, List<byte[]> allBlocks, Dictionary<byte, ComponentInfo> frameCompById)
    {
        int maxH = frameInfo.Components.Max(c => c.HorizontalSampling);
        int maxV = frameInfo.Components.Max(c => c.VerticalSampling);
        int mcuWidth = maxH * 8;
        int mcuHeight = maxV * 8;

        int mcusX = (frameInfo.Width + mcuWidth - 1) / mcuWidth;
        int mcusY = (frameInfo.Height + mcuHeight - 1) / mcuHeight;

        // Allocate component planes
        var planes = new Dictionary<int, byte[]>();
        var compWidths = new Dictionary<int, int>();
        var compHeights = new Dictionary<int, int>();

        foreach (var comp in frameInfo.Components)
        {
            int w = (frameInfo.Width * comp.HorizontalSampling + maxH - 1) / maxH;
            int h = (frameInfo.Height * comp.VerticalSampling + maxV - 1) / maxV;
            planes[comp.Id] = new byte[w * h];
            compWidths[comp.Id] = w;
            compHeights[comp.Id] = h;
        }

        int blockIndex = 0;
        for (int my = 0; my < mcusY; my++)
        {
            for (int mx = 0; mx < mcusX; mx++)
            {
                foreach (var scanComp in scanInfo.Components)
                {
                    byte compId = scanComp.ComponentId;
                    var comp = frameCompById[compId];
                    int H = comp.HorizontalSampling;
                    int V = comp.VerticalSampling;

                    for (int vy = 0; vy < V; vy++)
                    {
                        for (int hx = 0; hx < H; hx++)
                        {
                            var block = allBlocks[blockIndex++];
                            int planeW = compWidths[compId];
                            int planeH = compHeights[compId];
                            var plane = planes[compId];

                            // Calculate starting position in the component plane
                            int startX = (mx * H + hx) * 8;
                            int startY = (my * V + vy) * 8;

                            if (startX == 79 && startY == 1320)
                            {
                                //Console.WriteLine($"planeY before clamp = {plane[0]:F2}");
                            }

                            // Copy block to component plane
                            for (int by = 0; by < 8; by++)
                            {
                                int dstY = startY + by;
                                if (dstY >= planeH) continue;

                                for (int bx = 0; bx < 8; bx++)
                                {
                                    int dstX = startX + bx;
                                    if (dstX >= planeW) continue;

                                    plane[dstY * planeW + dstX] = block[by * 8 + bx];
                                }
                            }
                        }
                    }
                }
            }
        }

        // Get component planes
        byte[] planeY = planes[1];
        byte[] planeCb = planes[2];
        byte[] planeCr = planes[3];

        int yWidth = compWidths[1];
        int yHeight = compHeights[1];

        // Convert YCbCr to RGB using ImageSharp's likely coefficients
        var rgba = new byte[frameInfo.Width * frameInfo.Height * 4];
        for (int y = 0; y < frameInfo.Height; y++)
        {
            for (int x = 0; x < frameInfo.Width; x++)
            {
                int yIdx = y * yWidth + x;

                // Debug the specific problematic pixel
                if (x == 79 && y == 1320)
                {
                }

                float Y = planeY[yIdx];
                float Cb = planeCb[yIdx];
                float Cr = planeCr[yIdx];

                Cb -= 128f;
                Cr -= 128f;

                int r = Clamp((int)(Y + 1.40200 * Cr + 0.5), 0, 255);
                int g = Clamp((int)(Y - 0.34414 * Cb - 0.71414 * Cr + 0.5), 0, 255);
                int b = Clamp((int)(Y + 1.77200 * Cb + 0.5), 0, 255);

                //r = Clamp((int)(Y + 1.402 * (Cr)), 0, 255);
                //g = Clamp((int)(Y - 0.344136 * (Cb) - 0.714136 * (Cr)), 0, 255);
                //b = Clamp((int)(Y + 1.772 * (Cb)), 0, 255);

                // RGBA order
                int pixelIndex = (y * frameInfo.Width + x) * 4;
                rgba[pixelIndex] = (byte)r;
                rgba[pixelIndex + 1] = (byte)g;
                rgba[pixelIndex + 2] = (byte)b;
                rgba[pixelIndex + 3] = 255;
            }
        }

        return rgba;
    }
}