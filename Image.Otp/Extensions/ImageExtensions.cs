using Image.Otp.Models.Jpeg;
using Image.Otp.Parsers;
using System.Runtime.InteropServices;
using Image.Otp.Pixels;
using Image.Otp.Core.Formats;

namespace Image.Otp.Core.Extensions;

public static class ImageExtensions
{
    public static Image<T> Load<T>(string path) where T : unmanaged, IPixel<T>
    {
        using var fileStream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, FileOptions.SequentialScan);
        return Load<T>(fileStream);
    }

    public static Image<T> Load<T>(Stream stream) where T : unmanaged, IPixel<T>
    {
        var format = FormatResolver.Resolve(stream);

        return format switch
        {
            ImageFormat.Bmp => stream.LoadBmp<T>(),
            ImageFormat.Jpeg => stream.LoadJpeg<T>(),
            //ImageFormat.Png => stream.LoadBmp<T>(),
            _ => throw new NotSupportedException()
        };
    }

    private unsafe static Image<T> LoadJpeg<T>(byte[] bytes) where T : unmanaged, IPixel<T>
    {
        List<JpegSegment> segments = JpegParser.ParseSegmentsWithRestartMarkers(bytes);

        Dictionary<byte, QuantizationTable> qTables = JpegTableDecoder.ParseDqtSegments(segments);

        List<HuffmanTable> hTables = JpegTableDecoder.ParseDhtSegments(segments);

        JpegSegment frameSegment = segments.First(s => s.Marker == 0xC0 || s.Marker == 0xC2);
        var progressive = frameSegment.Marker == 0xC2;
        FrameInfo frameInfo = JpegTableDecoder.ParseSofSegment(frameSegment);

        var scanInfoss = JpegTableDecoder.ParseSosSegment(segments.First(s => s.Marker == 0xDA));

        var huffDc = new Dictionary<byte, CanonicalHuffmanTable>();
        var huffAc = new Dictionary<byte, CanonicalHuffmanTable>();
        foreach (var ht in hTables)
        {
            var table = HuffmanTableLogic.BuildCanonical(ht.CodeLengths, ht.Symbols);
            if (ht.Class == 0) huffDc[ht.Id] = table;
            else huffAc[ht.Id] = table;
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
                }
            }

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
            var scanInfo = JpegTableDecoder.ParseSosSegment(segments.First(s => s.Marker == 0xDA));
            var segment = segments.Single(s => s.Marker == 0x00);
            var mcus = JpegMcuDecoder.DecodeScanToBlocks(
                segment.Data,
                frameInfo,
                scanInfo,
                huffDc,
                huffAc,
                restartInterval);

            rgba = JpegProcessor.ProcessMCUBlocks(frameInfo, scanInfo, mcus, qTables);
        }

        var width = frameInfo.Width;
        var height = frameInfo.Height;

        var image = new Image<T>(width, height);

        var processor = PixelProcessorFactory.GetProcessor<T>();

        int bytesPerPixel = Marshal.SizeOf<T>();
        var sourceBytesPerPixel = bytesPerPixel;

        fixed (T* dstPtr = &image.Pixels[0])
        {
            fixed (byte* srcPtr = rgba)
            {
                int sourceStride = width * bytesPerPixel; // Assuming source has same layout

                for (int y = 0; y < height; y++)
                {
                    for (int x = 0; x < width; x++)
                    {
                        int srcPos = y * sourceStride + x * sourceBytesPerPixel;
                        int dstPos = y * width + x;

                        processor.ProcessPixel(srcPtr, srcPos, dstPtr, dstPos, sourceBytesPerPixel);
                    }
                }
            }
        }

        return image;
    }
}
