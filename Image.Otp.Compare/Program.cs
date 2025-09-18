using Image.Otp.Models.Jpeg;
using Image.Otp.Parsers;
using SixLabors.ImageSharp.PixelFormats;
using System.Drawing.Imaging;
using System.Drawing;
using Image.Otp;

class Program
{
    static async Task Main()
    {
        string filePath = @"C:\Users\User\source\repos\images\firstJpg.jpg";

        //HuffmanTrace.Run(filePath);

        // --- Load using SixLabors ---
        using var img = SixLabors.ImageSharp.Image.Load<Rgba32>(filePath);
        int width = img.Width;
        int height = img.Height;

        byte[] sharpPixels = new byte[width * height * 4]; // BGRA
        img.CopyPixelDataTo(sharpPixels);

        //
        using var bitmap = new Bitmap(filePath);

        // Lock the bitmap's bits
        Rectangle rect = new Rectangle(0, 0, bitmap.Width, bitmap.Height);
        BitmapData bmpData = bitmap.LockBits(rect, ImageLockMode.ReadOnly, bitmap.PixelFormat);

        // Calculate the number of bytes needed for the pixel data
        int bytesPerPixel = System.Drawing.Image.GetPixelFormatSize(bitmap.PixelFormat) / 8;
        int byteCount = bmpData.Stride * bitmap.Height;
        byte[] pixelData = new byte[byteCount];

        // Copy the RGB values into the byte array
        System.Runtime.InteropServices.Marshal.Copy(bmpData.Scan0, pixelData, 0, byteCount);

        byte[] bitmapPixels = new byte[bitmap.Width * bitmap.Height * 4]; // 3 bytes per pixel (R, G, B)

        // Fill the flat RGB array
        int rgbIndex = 0;
        for (int y = 0; y < bitmap.Height; y++)
        {
            for (int x = 0; x < bitmap.Width; x++)
            {
                int pixelIndex = y * bmpData.Stride + x * bytesPerPixel;

                // Assuming the pixel format is 24bpp RGB
                bitmapPixels[rgbIndex++] = pixelData[pixelIndex + 2]; // R
                bitmapPixels[rgbIndex++] = pixelData[pixelIndex + 1]; // G
                bitmapPixels[rgbIndex++] = pixelData[pixelIndex];     // B
                bitmapPixels[rgbIndex++] = 255;     // B
            }
        }

        // Unlock the bits
        bitmap.UnlockBits(bmpData);

        byte[] jpegBytes = File.ReadAllBytes(filePath);


        var segments = JpegParser.ParseSegmentsWithRestartMarkers(jpegBytes);

        JpegSegment frameSegment = segments.First(s => s.Marker == 0xC0 || s.Marker == 0xC2);
        var progressive = frameSegment.Marker == 0xC2;
        var frameInfo = JpegTableDecoder.ParseSofSegment(frameSegment);

        var scanInfo = JpegTableDecoder.ParseSosSegment(segments.First(s => s.Marker == 0xDA));

        //for (int i = 0; i < frameInfo.Components.Count; i++)
        //{
        //    var c = frameInfo.Components[i];
        //    var component = scanInfo.Components.First(a => a.ComponentId == c.Id);
        //    Console.WriteLine($" comp {i}: id={c.Id} hsamp={c.HorizontalSampling} " +
        //        $"vsamp={c.VerticalSampling} quant={c.QuantizationTableId} dc_tbl={component.DcHuffmanTableId} ac_tbl={component.AcHuffmanTableId}");
        //}

        var qTables = JpegTableDecoder.ParseDqtSegments(segments);

        // assume qTables is Dictionary<int, byte[64]>
        //foreach (var kv in qTables)
        //{
        //    Console.WriteLine($"DQT id={kv.Key}");
        //    var t = kv.Value;
        //    for (int i = 0; i < 64; i++)
        //    {
        //        Console.Write(t.Values[i] + (i % 8 == 7 ? "\n" : " "));
        //    }
        //}

        var hTables = JpegTableDecoder.ParseDhtSegments(segments);

        //foreach (var ht in hTables.OrderBy(c => c.Class))
        //{
        //    Console.WriteLine($"DHT class={ht.Class} id={ht.Id}");
        //    Console.Write("bits: ");
        //    for (int i = 1; i <= 16; i++) Console.Write(ht.CodeLengths[i - 1] + " ");
        //    Console.WriteLine();
        //    Console.Write("vals: ");
        //    foreach (var s in ht.Symbols) Console.Write(s.ToString("X2") + " ");
        //    Console.WriteLine();
        //}

        var huffDc = new Dictionary<byte, CanonicalHuffmanTable>();
        var huffAc = new Dictionary<byte, CanonicalHuffmanTable>();
        foreach (var ht in hTables)
        {
            var table = HuffmanTableLogic.BuildCanonical(ht.CodeLengths, ht.Symbols);
            if (ht.Class == 0)
            {
                //PrintTable(table);
                huffDc[ht.Id] = table;
            }
            else
            {
                //PrintCanonicalCodes(table, $"AC Table {table.Id}");
                huffAc[ht.Id] = table;
            }
        }

        var driData = segments.FirstOrDefault(c => c.Marker == 221)?.Data;
        var restartInterval = 0;
        if (driData is not null)
        {
            restartInterval = (driData[0] << 8) | driData[1];
        }

        byte[] rgba = [];
        if (progressive)
        {
            var sosDhtDataSegments = segments.Where(s => new byte[] { 0xDA, 0x00, 0xC4 }.Contains(s.Marker)).ToList();

            var currentTables = new Dictionary<(int Class, int Id), HuffmanTable>();

            List<MCUBlock>? currentMcus = null;
            ScanInfo? currentScanInfo = null;
            for (int i = 0; i < sosDhtDataSegments.Count; i++)
            {
                var segment = sosDhtDataSegments[i];

                if (segment.Marker == 0xC4)
                {
                    var dhtTable = JpegTableDecoder.ParseDhtSegments([segment]).First();
                    currentTables[(dhtTable.Class, dhtTable.Id)] = dhtTable;
                }
                if (segment.Marker == 0xDA)
                {
                    var sosSegment = segment;
                    currentScanInfo = JpegTableDecoder.ParseSosSegment(sosSegment);
                }
                if (segment.Marker == 0x00 && currentScanInfo is not null)
                {
                    var dataSegment = segment;

                    bool isDcOnlyScan = (currentScanInfo.Ss == 0 && currentScanInfo.Se == 0);

                    huffDc.Clear();
                    huffAc.Clear();

                    foreach (var component in currentScanInfo.Components)
                    {
                        if (!currentTables.TryGetValue((0, component.DcHuffmanTableId), out var dcTable))
                            throw new InvalidOperationException($"DC Huffman table {component.DcHuffmanTableId} not found");

                        if (!currentTables.TryGetValue((1, component.AcHuffmanTableId), out var acTable) && !isDcOnlyScan)
                        {
                            throw new InvalidOperationException($"AC Huffman table {component.AcHuffmanTableId} not found");
                        }

                        huffDc[component.DcHuffmanTableId] = HuffmanTableLogic.BuildCanonical(dcTable.CodeLengths, dcTable.Symbols);
                        if (acTable is not null)
                        {
                            huffAc[component.AcHuffmanTableId] = HuffmanTableLogic.BuildCanonical(acTable.CodeLengths, acTable.Symbols);
                        }
                    }

                    currentMcus = JpegMcuDecoder.DecodeScanToBlocksProgressive(
                        dataSegment.Data, frameInfo, currentScanInfo,
                        huffDc, huffAc, restartInterval, currentMcus);

                    //JpegHelpres.PrintAllComponentsLikeC(currentMcus);

                    //rgba = JpegProcessor.ProcessMCUBlocks(frameInfo, currentMcus, qTables);

                    //SaveRgba32ToBmp(rgba, width, height, $@"{Path.GetDirectoryName(filePath)}\{DateTime.UtcNow.Ticks}-inremidiet-{i}.bmp");
                }
            }

            //var mcus = MCUBlockParser.ParseFile("C:\\Users\\User\\source\\repos\\dump\\mcus.txt");

            foreach (var mcu in currentMcus)
            {
                foreach (var blocks in mcu.ComponentBlocks)
                {
                    for (int i = 0; i < blocks.Value.Count; i++)
                    {
                        var block = blocks.Value[i];
                        blocks.Value[i] = JpegDecoderHelpers.NaturalToZigzag(blocks.Value[i]);
                    }
                }
            }

            rgba = JpegProcessor.ProcessMCUBlocks(frameInfo, currentMcus, qTables);
        }
        else
        {
            var segment = segments.Single(s => s.Marker == 0x00);
            var mcus = JpegMcuDecoder.DecodeScanToBlocks(
                segment.Data,
                frameInfo,
                scanInfo,
                huffDc,
                huffAc,
                restartInterval);

            //JpegHelpres.PrintAllComponentsLikeC(mcus);

            //foreach (var item in mcus)
            //{
            //    foreach (var block in item.ComponentBlocks)
            //    {
            //        foreach (var bloks in block.Value)
            //        {
            //            for (int i = 1; i < bloks.Length; i++)
            //            {
            //                bloks[i] = 0;
            //            }
            //        }
            //    }
            //}

            rgba = JpegProcessor.ProcessMCUBlocks(frameInfo, scanInfo, mcus, qTables);

            //SaveRgba32ToBmp(rgba, width, height, $@"{Path.GetDirectoryName(filePath)}\{DateTime.UtcNow.Ticks}-test.bmp");
        }

        SaveRgba32ToBmp(bitmapPixels, width, height, $@"{Path.GetDirectoryName(filePath)}\{DateTime.UtcNow.Ticks}-bitmap.bmp");
        SaveRgba32ToBmp(sharpPixels, width, height, $@"{Path.GetDirectoryName(filePath)}\{DateTime.UtcNow.Ticks}-sharp.bmp");
        SaveRgba32ToBmp(rgba, width, height, $@"{Path.GetDirectoryName(filePath)}\{DateTime.UtcNow.Ticks}-mine.bmp");

        for (int i = 0; i < width * height / 10; i++)
        {
            if (sharpPixels[i] != rgba[i] || rgba[i] != bitmapPixels[i])
            {
                Console.WriteLine($"Mismatch at index {i}: sharp={sharpPixels[i]}, mine={rgba[i]}, bitmap={bitmapPixels[i]}");
            }
        }
    }

    public static byte[] ProcessMCUBlocks(FrameInfo frameInfo, ScanInfo scanInfo, List<MCUBlock> compressedData, Dictionary<byte, QuantizationTable> qTables)
    {
        var frameCompById = frameInfo.Components.ToDictionary(c => c.Id);
        var allBlocks = new List<byte[]>();

        foreach (var mcu in compressedData)
        {
            // Use scan order, not frame order
            foreach (var scanComp in scanInfo.Components)
            {
                byte compId = scanComp.ComponentId;
                var comp = frameCompById[compId];

                if (!mcu.ComponentBlocks.TryGetValue(compId, out var blockList))
                    continue;

                var quantTable = qTables[comp.QuantizationTableId];

                foreach (var coeffs in blockList)
                {
                    var pixelBlock = JpegIdct.DequantizeInverseZigZagIdct(coeffs, quantTable.Values);
                    allBlocks.Add(pixelBlock);
                }
            }
        }

        int maxH = frameInfo.Components.Max(c => c.HorizontalSampling);
        int maxV = frameInfo.Components.Max(c => c.VerticalSampling);
        int mcuWidth = maxH * 8;
        int mcuHeight = maxV * 8;

        int mcusX = (frameInfo.Width + mcuWidth - 1) / mcuWidth;
        int mcusY = (frameInfo.Height + mcuHeight - 1) / mcuHeight;

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

        static void UpsampleNearest(byte[] src, byte[] dst, int srcW, int srcH, int dstW, int dstH)
        {
            for (int y = 0; y < dstH; y++)
            {
                int sy = y * srcH / dstH;
                for (int x = 0; x < dstW; x++)
                {
                    int sx = x * srcW / dstW;
                    dst[y * dstW + x] = src[sy * srcW + sx];
                }
            }
        }


        if (compWidths[2] != frameInfo.Width || compHeights[2] != frameInfo.Height)
        {
            var upCb = new byte[frameInfo.Width * frameInfo.Height];
            UpsampleNearest(planeCb, upCb, compWidths[2], compHeights[2], frameInfo.Width, frameInfo.Height);
            planeCb = upCb;
        }

        if (compWidths[3] != frameInfo.Width || compHeights[3] != frameInfo.Height)
        {
            var upCr = new byte[frameInfo.Width * frameInfo.Height];
            UpsampleNearest(planeCr, upCr, compWidths[3], compHeights[3], frameInfo.Width, frameInfo.Height);
            planeCr = upCr;
        }

        int yWidth = compWidths[1];
        int yHeight = compHeights[1];
        int cbWidth = compWidths[2];
        int cbHeight = compHeights[2];
        int crWidth = compWidths[3];
        int crHeight = compHeights[3];

        // Convert YCbCr to RGB using ImageSharp's likely coefficients
        var rgba = new byte[frameInfo.Width * frameInfo.Height * 4];

        for (int y = 0; y < frameInfo.Height; y++)
        {
            for (int x = 0; x < frameInfo.Width; x++)
            {
                // Y is full resolution
                int yIdx = y * yWidth + x;
                float Y = planeY[yIdx];

                // Map luma coords to chroma coords (nearest-neighbor upsampling)
                int cbX = x * cbWidth / yWidth;
                int cbY = y * cbHeight / yHeight;
                int crX = x * crWidth / yWidth;
                int crY = y * crHeight / yHeight;

                float Cb = planeCb[cbY * cbWidth + cbX];
                float Cr = planeCr[crY * crWidth + crX];

                //YuvToRgb((byte)Y, (byte)Cb, (byte)Cr, out var r, out var g, out var b);

                Cb -= 128f;
                Cr -= 128f;

                int r = Clamp((int)(Y + 1.40200 * Cr + 0.5), 0, 255);
                int g = Clamp((int)(Y - 0.34414 * Cb - 0.71414 * Cr + 0.5), 0, 255);
                int b = Clamp((int)(Y + 1.77200 * Cb + 0.5), 0, 255);

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

    static int Clamp(int value, int min, int max) => value < min ? min : value > max ? max : value;

    public static void PrintTable(CanonicalHuffmanTable huffmanTable)
    {
        for (int length = 1; length <= 16; length++)
        {
            var dict = huffmanTable._byLength[length];
            if (dict.Count == 0) continue;

            int minCode = dict.Keys.Min();
            int maxCode = dict.Keys.Max();

            // Convert to binary strings with exactly 'length' bits
            string minBinary = Convert.ToString(minCode, 2).PadLeft(length, '0');
            string maxBinary = Convert.ToString(maxCode, 2).PadLeft(length, '0');

            Console.WriteLine($"  Length {length}: codes {minBinary} to {maxBinary} ({dict.Count} codes)");

            // Optional: show each code and its symbol
            foreach (var kvp in dict.OrderBy(x => x.Key))
            {
                string binaryCode = Convert.ToString(kvp.Key, 2).PadLeft(length, '0');
                Console.WriteLine($"    {binaryCode} -> Value {kvp.Value:X2}");
            }
        }
    }

    // Additional method to print canonical codes in the same format as the C function
    static void PrintCanonicalCodes(CanonicalHuffmanTable table, string tableName)
    {
        Console.WriteLine($"=== {tableName} Canonical Codes ===");
        Console.WriteLine("Length -> Code range:");

        int code = 0;
        for (int length = 1; length <= 16; length++)
        {
            int count = table._byLength[length].Count;
            if (count > 0)
            {
                Console.WriteLine($"  Length {length}: codes {code:X4} to {code + count - 1:X4} ({count} codes)");

                // Print individual code-symbol mappings
                //foreach (var entry in table._byLength[length])
                //{
                //    Console.WriteLine($"    Code {entry.Key:X4} (length {length}) -> Symbol {entry.Value}");
                //}
            }
            code = (code + count) << 1;
        }
        Console.WriteLine();
    }

    static void SaveRgba32ToBmp(byte[] rgba, int width, int height, string filePath)
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
