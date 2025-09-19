using Image.Otp.Primitives;
using System.Runtime.CompilerServices;
using Image.Otp.Models.Jpeg;
using Image.Otp.Parsers;
using System.Runtime.InteropServices;
using Image.Otp.Pixels;
using Image.Otp.Helpers;
using Image.Otp.Constants;
using System.Buffers;

namespace Image.Otp.Extensions;

public static class ImageExtensions
{
    public static Image<T> Load<T>(string path) where T : unmanaged, IPixel<T>
    {
        byte[] bytes = File.ReadAllBytes(path);
        return LoadFromMemory<T>(bytes);
    }

    public static Image<T> LoadFromMemory<T>(byte[] fileData) where T : unmanaged, IPixel<T>
    {
        using var ms = new MemoryStream(fileData);
        using var br = new BinaryReader(ms);

        var signature = System.Text.Encoding.ASCII.GetString(br.ReadBytes(2));
        ms.Position = 0;

        const string bmSignature = "BM";

        return signature switch
        {
            bmSignature => LoadBmp<T>(br),
            //_ when IsPng(fileData) => LoadPng(br),
            _ when IsJpeg(fileData) => LoadJpeg<T>(fileData),
            _ => throw new NotSupportedException("Unsupported image format")
        };
    }

    private unsafe static Image<T> LoadBmp<T>(BinaryReader br) where T : unmanaged
    {
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

                        //if (typeof(T) == typeof(Rgb24))
                        //{
                        //    ((Rgb24*)dstPtr)[dstPos] = new Rgb24(
                        //        srcPtr[srcPos + 2], // R
                        //        srcPtr[srcPos + 1], // G
                        //        srcPtr[srcPos + 0]  // B
                        //    );
                        //}
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

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool IsJpeg(byte[] data) => data.Length > 2 && data[0] == 0xFF && data[1] == 0xD8;

    public static Image<T> LoadJpegBase<T>(string path) where T : unmanaged, IPixel<T>
    {
        byte[] bytes = File.ReadAllBytes(path);
        return LoadJpeg<T>(bytes);
    }

    public static Image<T> LoadJpegMemory<T>(string path) where T : unmanaged, IPixel<T>
    {
        using var fileStream = new FileStream(path, FileMode.Open);
        return LoadJpeg<T>(fileStream);
    }

    public static ImageOtp<T> LoadJpegNative<T>(string path) where T : unmanaged, IPixel<T>
    {
        using var fileStream = new FileStream(path, FileMode.Open);
        return LoadJpegNative<T>(fileStream);
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

    private unsafe static Image<T> LoadJpeg<T>(Stream stream) where T : unmanaged, IPixel<T>
    {
        var combiner = new Combiner();

        while (stream.CanRead)
        {
            var byteRead = stream.ReadByte();

            if (byteRead == JpegMarkers.FF)
            {
                byteRead = stream.ReadByte();

                if (byteRead == JpegMarkers.EOI)
                {
                    break;
                }

                if (JpegMarkers.HasLengthData(byteRead))
                {
                    var firstPart = stream.ReadByte();
                    var secondPart = stream.ReadByte();

                    var length = (firstPart << 8) | secondPart;
                    length -= 2;

                    ProcessMarker(stream, byteRead, length, combiner);
                }
            }
        }

        var rgba = JpegProcessor.ProcessMCUBlocks(combiner.FrameInfo, combiner.ScanInfo, combiner.MCUs, combiner.QuantizationTables);

        var width = combiner.FrameInfo.Width;
        var height = combiner.FrameInfo.Height;

        var image = new Image<T>(width, height);

        var processor = PixelProcessorFactory.GetProcessor<T>();

        int bytesPerPixel = Marshal.SizeOf<T>();
        var sourceBytesPerPixel = bytesPerPixel;

        fixed (T* dstPtr = &image.Pixels[0])
        {
            fixed (byte* srcPtr = rgba)
            {
                int sourceStride = width * bytesPerPixel;

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

    private unsafe static ImageOtp<T> LoadJpegNative<T>(Stream stream) where T : unmanaged, IPixel<T>
    {
        var combiner = new Combiner();

        while (stream.CanRead)
        {
            var byteRead = stream.ReadByte();

            if (byteRead == JpegMarkers.FF)
            {
                byteRead = stream.ReadByte();

                if (byteRead == JpegMarkers.EOI)
                {
                    break;
                }

                if (JpegMarkers.HasLengthData(byteRead))
                {
                    var firstPart = stream.ReadByte();
                    var secondPart = stream.ReadByte();

                    var length = (firstPart << 8) | secondPart;
                    length -= 2;

                    ProcessMarker(stream, byteRead, length, combiner);
                }
            }
        }

        var rgba = JpegProcessor.ProcessMCUBlocks(combiner.FrameInfo, combiner.ScanInfo, combiner.MCUs, combiner.QuantizationTables);

        var width = combiner.FrameInfo.Width;
        var height = combiner.FrameInfo.Height;

        var image = new ImageOtp<T>(width, height);

        var processor = PixelProcessorFactory.GetProcessor<T>();

        int bytesPerPixel = Marshal.SizeOf<T>();
        var sourceBytesPerPixel = bytesPerPixel;

        fixed (T* dstPtr = &image.Pixels[0])
        {
            fixed (byte* srcPtr = rgba)
            {
                int sourceStride = width * bytesPerPixel;

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

    public sealed class Combiner
    {
        public FrameInfo FrameInfo { get; set; }
        public ScanInfo ScanInfo { get; set; }

        public Dictionary<byte, QuantizationTable> QuantizationTables = [];

        public List<HuffmanTable> HuffmanTables = [];

        public int RestartInterval { get; set; }

        public List<MCUBlock> MCUs = [];
    }

    private static void ProcessMarker(Stream stream, int marker, int length, Combiner combiner)
    {
        var endPosition = stream.Position + length;
        switch (marker)
        {
            case JpegMarkers.DQT:
                ProcessDQT(stream, combiner, endPosition);
                break;
            case JpegMarkers.SOS:
                ProcessSOS(stream, length, combiner);
                break;
            case JpegMarkers.DRI:
                break;
            case JpegMarkers.DHT:
                ParseDhtSegments(stream, endPosition, combiner);
                break;
            case JpegMarkers.SOF0:
            case JpegMarkers.SOF2:
                ProcessSOF(stream, marker, length, combiner);
                break;
            default:
                stream.Seek(length, SeekOrigin.Current);
                break;
        }
    }

    private static void ParseDhtSegments(Stream stream, long endPosition, Combiner combiner)
    {
        while (stream.Position < endPosition)
        {
            var tcTh = stream.ReadByte();
            var tc = (byte)((tcTh >> 4) & 0x0F);
            var th = (byte)(tcTh & 0x0F);

            const int CodeLengths = 16;
            var lengths = new byte[CodeLengths];
            stream.ReadExactly(lengths, 0, CodeLengths);

            int symbolCount = 0;
            for (int i = 0; i < CodeLengths; i++)
                symbolCount += lengths[i];

            var symbols = new byte[symbolCount];
            stream.ReadExactly(symbols, 0, symbolCount);

            combiner.HuffmanTables.Add(new HuffmanTable
            {
                Class = tc,
                Id = th,
                CodeLengths = lengths,
                Symbols = symbols
            });
        }
    }

    private static void ProcessSOS(Stream stream, int length, Combiner combiner)
    {
        if (length < 6)
            throw new InvalidDataException("SOS segment too short.");

        var pos = 0;
        var numComponents = stream.ReadByte();

        // Validate component count
        if (numComponents < 1 || numComponents > 4)
            throw new InvalidDataException($"Invalid number of components: {numComponents}");

        // Validate segment length: 1 byte (numComponents) + 2*numComponents + 3 bytes (Ss, Se, AhAl)
        if (length != 1 + 2 * numComponents + 3)
            throw new InvalidDataException("SOS segment length mismatch.");

        var components = new List<ScanComponent>();
        for (int i = 0; i < numComponents; i++)
        {
            if (pos + 1 >= length)
                throw new InvalidDataException("Unexpected end of SOS segment.");

            var componentId = (byte)stream.ReadByte();
            var huffmanTableIds = (byte)stream.ReadByte();
            components.Add(new ScanComponent
            {
                ComponentId = componentId,
                DcHuffmanTableId = (byte)(huffmanTableIds >> 4),
                AcHuffmanTableId = (byte)(huffmanTableIds & 0x0F)
            });
            pos++;
        }

        // Read spectral parameters
        byte ss = (byte)stream.ReadByte();
        byte se = (byte)stream.ReadByte();
        byte ahAl = (byte)stream.ReadByte();
        byte ah = (byte)(ahAl >> 4);
        byte al = (byte)(ahAl & 0x0F);

        var huff = new Dictionary<(byte Class, byte Id), CanonicalHuffmanTable>(combiner.HuffmanTables.Count);
        foreach (var ht in combiner.HuffmanTables)
        {
            var table = HuffmanTableLogic.BuildCanonical(ht.CodeLengths, ht.Symbols);
            huff[(ht.Class, ht.Id)] = table;
        }

        combiner.ScanInfo = new ScanInfo
        {
            Components = components,
            Ss = ss,
            Se = se,
            Ah = ah,
            Al = al
        };

        DecodeScanToBlocks(stream, huff, combiner);
    }

    private static void DecodeScanToBlocksCombinedWithIDCT(
        Stream stream,
        Dictionary<(byte Class, byte Id), CanonicalHuffmanTable> huff,
        Combiner combiner)
    {
        ArgumentNullException.ThrowIfNull(stream);
        ArgumentNullException.ThrowIfNull(combiner);
        ArgumentNullException.ThrowIfNull(combiner.FrameInfo);
        ArgumentNullException.ThrowIfNull(combiner.ScanInfo);

        var frameInfo = combiner.FrameInfo;
        var scan = combiner.ScanInfo;
        var restartInterval = combiner.RestartInterval;

        if (scan.Ss != 0 || scan.Se != 63 || scan.Ah != 0 || scan.Al != 0)
            throw new ArgumentException("This decoder only supports baseline non-progressive scans (Ss=0,Se=63,Ah=0,Al=0).");

        var compMap = frameInfo.Components.ToDictionary(c => c.Id);

        var scanComponents = scan.Components;

        int maxH = frameInfo.Components.Max(c => c.HorizontalSampling);
        int maxV = frameInfo.Components.Max(c => c.VerticalSampling);

        int mcuCols = (frameInfo.Width + (8 * maxH - 1)) / (8 * maxH);
        int mcuRows = (frameInfo.Height + (8 * maxV - 1)) / (8 * maxV);

        var dcPredictor = new Dictionary<byte, int>(scanComponents.Count);
        foreach (var sc in scanComponents) dcPredictor[sc.ComponentId] = 0;

        int restartCounter = restartInterval;
        int expectedRst = 0; // 0..7 for RST0..RST7

        var bitReader = new StreamBitReader(stream);

        var componentBuffers = new Dictionary<byte, byte[]>();
        foreach (var comp in frameInfo.Components)
        {
            componentBuffers[comp.Id] = new byte[frameInfo.Width * frameInfo.Height];
            Array.Fill(componentBuffers[comp.Id], (byte)128);
        }

        int width = frameInfo.Width;
        int height = frameInfo.Height;

        var result = new ImageOtp<Rgba32>(width, height);
        var output = result.Pixels;
        for (int my = 0; my < mcuRows; my++)
        {
            for (int mx = 0; mx < mcuCols; mx++)
            {
                if (restartInterval > 0 && restartCounter == 0 && (mx != 0 || my != 0))
                {
                    RestartInterval(restartInterval, dcPredictor, out restartCounter, out expectedRst, bitReader);
                }

                foreach (var sc in scanComponents)
                {
                    var comp = compMap[sc.ComponentId];

                    if (!combiner.QuantizationTables.TryGetValue(comp.QuantizationTableId, out var qTable))
                    {
                        throw new InvalidOperationException($"Quantization table {comp.QuantizationTableId} not found.");
                    }

                    int h = comp.HorizontalSampling;
                    int v = comp.VerticalSampling;

                    byte[] compBuffer = componentBuffers[comp.Id];

                    int blocksPerMcu = h * v;

                    var dcTable = huff[(0, sc.DcHuffmanTableId)];
                    var acTable = huff[(1, sc.AcHuffmanTableId)];

                    int scaleX = maxH / h;
                    int scaleY = maxV / v;

                    for (int by = 0; by < v; by++)
                    {
                        for (int bx = 0; bx < h; bx++)
                        {
                            short[] block = new short[64];

                            block[0] = GetDc(dcPredictor, bitReader, sc, dcTable);

                            SetAc(bitReader, acTable, block);

                            block = JpegDecoderHelpers.NaturalToZigzag(block);

                            double[] dequant = JpegHelpres.DequantizeBlock(block, qTable);
                            double[] samples = new double[64];
                            JpegHelpres.InverseDCT8x8(dequant, samples);

                            int blockOriginX = mx * maxH * 8 + bx * 8 * scaleX;
                            int blockOriginY = my * maxV * 8 + by * 8 * scaleY;

                            for (int sy = 0; sy < 8; sy++)
                            {
                                for (int sx = 0; sx < 8; sx++)
                                {
                                    double sampleValue = samples[sy * 8 + sx] + 128.0;
                                    int sampleInt = (int)Math.Round(sampleValue);
                                    sampleInt = Math.Clamp(sampleInt, 0, 255);

                                    for (int uy = 0; uy < scaleY; uy++)
                                    {
                                        int outY = blockOriginY + sy * scaleY + uy;
                                        if (outY < 0 || outY >= height) continue;
                                        for (int ux = 0; ux < scaleX; ux++)
                                        {
                                            int outX = blockOriginX + sx * scaleX + ux;
                                            if (outX < 0 || outX >= width) continue;

                                            int pixelIndex = outY * width + outX;
                                            compBuffer[pixelIndex] = (byte)sampleInt;
                                        }
                                    }
                                }
                            }
                        }
                    }
                }

                if (restartInterval > 0)
                    restartCounter = Math.Max(--restartCounter, 0);
            }
        }

        byte[] yBuffer = componentBuffers[1];
        byte[] cbBuffer = componentBuffers.ContainsKey(2) ? componentBuffers[2] : null;
        byte[] crBuffer = componentBuffers.ContainsKey(3) ? componentBuffers[3] : null;

        for (int i = 0; i < width * height; i++)
        {
            byte yVal = yBuffer[i];
            byte cbVal = cbBuffer != null ? cbBuffer[i] : (byte)128;
            byte crVal = crBuffer != null ? crBuffer[i] : (byte)128;

            double Yd = yVal;
            double Cbd = cbVal - 128.0;
            double Crd = crVal - 128.0;

            int r = (int)Math.Round(Yd + 1.402 * Crd);
            int g = (int)Math.Round(Yd - 0.344136 * Cbd - 0.714136 * Crd);
            int b = (int)Math.Round(Yd + 1.772 * Cbd);

            r = Math.Clamp(r, 0, 255);
            g = Math.Clamp(g, 0, 255);
            b = Math.Clamp(b, 0, 255);

            output[i] = new Rgba32((byte)r, (byte)g, (byte)b);
        }

        result.SaveAsBmp("C:\\Users\\User\\source\\repos\\images\\3.bmp");

        static short GetDc(Dictionary<byte, int> dcPredictor, StreamBitReader bitReader, ScanComponent sc, CanonicalHuffmanTable dcTable)
        {
            var sym = JpegHelpres.DecodeHuffmanSymbol(bitReader, dcTable);
            if (sym < 0)
                throw new EndOfStreamException("Marker or EOF encountered while decoding DC.");

            var magnitude = sym; // number of additional bits
            var dcDiff = 0;

            if (magnitude > 0)
            {
                var bits = bitReader.ReadBits(magnitude, false);
                if (bits < 0)
                    throw new EndOfStreamException("EOF/marker while reading DC bits.");

                dcDiff = JpegDecoderHelpers.ExtendSign(bits, magnitude);
            }

            var prevDc = dcPredictor[sc.ComponentId];
            var dcVal = prevDc + dcDiff;
            dcPredictor[sc.ComponentId] = dcVal;
            return (short)dcVal;
        }

        static void SetAc(StreamBitReader bitReader, CanonicalHuffmanTable acTable, short[] block)
        {
            int k = 1;
            while (k < 64)
            {
                int acSym = JpegHelpres.DecodeHuffmanSymbol(bitReader, acTable);
                if (acSym < 0)
                    throw new EndOfStreamException("Marker or EOF encountered while decoding AC.");

                if (acSym == 0x00)
                {
                    break;
                }
                if (acSym == 0xF0)
                {
                    k += 16;
                    continue;
                }
                int run = (acSym >> 4) & 0x0F;
                int size = acSym & 0x0F;
                k += run;
                if (k >= 64)
                    throw new InvalidDataException("Run exceeds block size while decoding AC.");

                int bits = 0;
                if (size > 0)
                {
                    bits = bitReader.ReadBits(size, false);
                    if (bits < 0) throw new EndOfStreamException("EOF/marker while reading AC bits.");
                }

                int level = JpegDecoderHelpers.ExtendSign(bits, size);
                block[k] = (short)level;
                k++;
            }
        }

        static void RestartInterval(int restartInterval, Dictionary<byte, int> dcPredictor, out int restartCounter, out int expectedRst, StreamBitReader bitReader)
        {
            bitReader.AlignToByte();

            var marker = JpegHelpres.FindNextMarker(bitReader);
            if (marker < 0)
                throw new EndOfStreamException("Unexpected EOF while searching for restart marker.");
            if (marker < 0xD0 || marker > 0xD7)
                throw new InvalidDataException($"Expected restart marker RSTn but found 0xFF{marker:X2}.");

            expectedRst = marker - 0xD0;

            foreach (var k in dcPredictor.Keys)
                dcPredictor[k] = 0;

            expectedRst = (expectedRst + 1) & 7;
            restartCounter = restartInterval;
        }
    }


    private static void DecodeScanToBlocks(
        Stream stream,
        Dictionary<(byte Class, byte Id), CanonicalHuffmanTable> huff,
        Combiner combiner)
    {
        ArgumentNullException.ThrowIfNull(stream);
        ArgumentNullException.ThrowIfNull(combiner);
        ArgumentNullException.ThrowIfNull(combiner.FrameInfo);
        ArgumentNullException.ThrowIfNull(combiner.ScanInfo);

        var frame = combiner.FrameInfo;
        var scan = combiner.ScanInfo;
        var restartInterval = combiner.RestartInterval;

        if (scan.Ss != 0 || scan.Se != 63 || scan.Ah != 0 || scan.Al != 0)
            throw new ArgumentException("This decoder only supports baseline non-progressive scans (Ss=0,Se=63,Ah=0,Al=0).");

        var compMap = frame.Components.ToDictionary(c => c.Id);

        var scanComponents = scan.Components;

        int maxH = frame.Components.Max(c => c.HorizontalSampling);
        int maxV = frame.Components.Max(c => c.VerticalSampling);

        int mcuCols = (frame.Width + (8 * maxH - 1)) / (8 * maxH);
        int mcuRows = (frame.Height + (8 * maxV - 1)) / (8 * maxV);

        var dcPredictor = new Dictionary<byte, int>(scanComponents.Count);
        foreach (var sc in scanComponents) dcPredictor[sc.ComponentId] = 0;

        int restartCounter = restartInterval;
        int expectedRst = 0; // 0..7 for RST0..RST7

        var bitReader = new StreamBitReader(stream);

        var result = new List<MCUBlock>(mcuRows * mcuCols);
        for (int my = 0; my < mcuRows; my++)
        {
            for (int mx = 0; mx < mcuCols; mx++)
            {
                if (restartInterval > 0 && restartCounter == 0 && (mx != 0 || my != 0))
                {
                    RestartInterval(restartInterval, dcPredictor, out restartCounter, out expectedRst, bitReader);
                }

                var mcu = new MCUBlock { X = mx, Y = my };

                foreach (var sc in scanComponents)
                {
                    var comp = compMap[sc.ComponentId];
                    int h = comp.HorizontalSampling;
                    int v = comp.VerticalSampling;

                    int blocksPerMcu = h * v;
                    if (!mcu.ComponentBlocks.TryGetValue(sc.ComponentId, out var list))
                    {
                        list = new List<short[]>(blocksPerMcu);
                        mcu.ComponentBlocks[sc.ComponentId] = list;
                    }

                    var dcTable = huff[(0, sc.DcHuffmanTableId)];
                    var acTable = huff[(1, sc.AcHuffmanTableId)];

                    for (int b = 0; b < blocksPerMcu; b++)
                    {
                        short[] block = new short[64];

                        block[0] = GetDc(dcPredictor, bitReader, sc, dcTable);

                        SetAc(bitReader, acTable, block);

                        block = JpegDecoderHelpers.NaturalToZigzag(block);
                        list.Add(block);
                    }
                }

                result.Add(mcu);

                if (restartInterval > 0)
                    restartCounter = Math.Max(--restartCounter, 0);
            }
        }

        combiner.MCUs = result;

        static short GetDc(Dictionary<byte, int> dcPredictor, StreamBitReader bitReader, ScanComponent sc, CanonicalHuffmanTable dcTable)
        {
            var sym = JpegHelpres.DecodeHuffmanSymbol(bitReader, dcTable);
            if (sym < 0)
                throw new EndOfStreamException("Marker or EOF encountered while decoding DC.");

            var magnitude = sym; // number of additional bits
            var dcDiff = 0;

            if (magnitude > 0)
            {
                var bits = bitReader.ReadBits(magnitude, false);
                if (bits < 0)
                    throw new EndOfStreamException("EOF/marker while reading DC bits.");

                dcDiff = JpegDecoderHelpers.ExtendSign(bits, magnitude);
            }

            var prevDc = dcPredictor[sc.ComponentId];
            var dcVal = prevDc + dcDiff;
            dcPredictor[sc.ComponentId] = dcVal;
            return (short)dcVal;
        }

        static void SetAc(StreamBitReader bitReader, CanonicalHuffmanTable acTable, short[] block)
        {
            int k = 1;
            while (k < 64)
            {
                int acSym = JpegHelpres.DecodeHuffmanSymbol(bitReader, acTable);
                if (acSym < 0)
                    throw new EndOfStreamException("Marker or EOF encountered while decoding AC.");

                if (acSym == 0x00)
                {
                    break;
                }
                if (acSym == 0xF0)
                {
                    k += 16;
                    continue;
                }
                int run = (acSym >> 4) & 0x0F;
                int size = acSym & 0x0F;
                k += run;
                if (k >= 64)
                    throw new InvalidDataException("Run exceeds block size while decoding AC.");

                int bits = 0;
                if (size > 0)
                {
                    bits = bitReader.ReadBits(size, false);
                    if (bits < 0) throw new EndOfStreamException("EOF/marker while reading AC bits.");
                }

                int level = JpegDecoderHelpers.ExtendSign(bits, size);
                block[k] = (short)level;
                k++;
            }
        }

        static void RestartInterval(int restartInterval, Dictionary<byte, int> dcPredictor, out int restartCounter, out int expectedRst, StreamBitReader bitReader)
        {
            bitReader.AlignToByte();

            var marker = JpegHelpres.FindNextMarker(bitReader);
            if (marker < 0)
                throw new EndOfStreamException("Unexpected EOF while searching for restart marker.");
            if (marker < 0xD0 || marker > 0xD7)
                throw new InvalidDataException($"Expected restart marker RSTn but found 0xFF{marker:X2}.");

            expectedRst = marker - 0xD0;

            foreach (var k in dcPredictor.Keys)
                dcPredictor[k] = 0;

            expectedRst = (expectedRst + 1) & 7;
            restartCounter = restartInterval;
        }
    }

    private static void ProcessSOF(Stream stream, int marker, int length, Combiner combiner)
    {
        //TODO: Expand valid SOF markers to include all standard SOF markers
        if (marker < 0xC0 || marker > 0xCF || marker == 0xC4 || marker == 0xC8 || marker == 0xCC)
            throw new InvalidDataException("Invalid SOF marker.");

        //TODO: Verify minimum length: precision(1) + dimensions(4) + numComponents(1) + components*3
        if (length < 6)
            throw new InvalidDataException("SOF segment too short.");

        var precision = (byte)stream.ReadByte(); // Read precision (usually 8)

        var firstPart = stream.ReadByte();
        var secondPart = stream.ReadByte();
        //TODO: int height = (seg.Data[pos++] << 8) | seg.Data[pos++];
        int height = (firstPart * 256) | secondPart;

        firstPart = stream.ReadByte();
        secondPart = stream.ReadByte();
        //TODO: int width = (seg.Data[pos++] << 8) | seg.Data[pos++];
        int width = (firstPart * 256) | secondPart;

        var numComponents = stream.ReadByte();
        //TODO: Check if there's enough data for all components
        if (length < 6 + 3 * numComponents)
            throw new InvalidDataException("SOF segment too short for components.");

        var components = new ComponentInfo[numComponents];
        for (int i = 0; i < numComponents; i++)
        {
            var id = (byte)stream.ReadByte();
            var samplingFactor = (byte)stream.ReadByte();
            components[i] = new ComponentInfo
            {
                Id = id,
                SamplingFactor = samplingFactor,
                HorizontalSampling = (byte)(samplingFactor >> 4),   // Upper 4 bits
                VerticalSampling = (byte)(samplingFactor & 0x0F),   // Lower 4 bits
                QuantizationTableId = (byte)stream.ReadByte()
            };
        }

        var frameInfo = new FrameInfo
        {
            Precision = precision, // Include precision in output
            Width = width,
            Height = height,
            Components = components
        };

        combiner.FrameInfo = frameInfo;
    }

    private static void ProcessDQT(Stream stream, Combiner combiner, long endPosition)
    {
        var tables = combiner.QuantizationTables;

        while (stream.Position < endPosition)
        {
            var pqTq = stream.ReadByte();

            var pq = (pqTq >> 4) & 0x0F; // precision: 0 = 8-bit, 1 = 16-bit
            var tq = (byte)(pqTq & 0x0F); // table id

            // Validate table ID
            if (tq > 3)
                throw new InvalidDataException($"DQT table ID {tq} is invalid. Must be 0-3.");

            if (pq != 0 && pq != 1)
                throw new InvalidDataException($"Unsupported DQT precision {pq} in table {tq}.");

            const int DqtTableSize = 64;
            //var raw = new ushort[DqtTableSize];
            var raw = ArrayPool<ushort>.Shared.Rent(DqtTableSize);

            if (pq == 0)
            {
                for (int i = 0; i < DqtTableSize; i++)
                    raw[i] = (ushort)stream.ReadByte();
            }
            else
            {
                for (int i = 0; i < DqtTableSize; i++)
                {
                    var firstPart = stream.ReadByte();
                    var secondPart = stream.ReadByte();
                    //raw[i] = (ushort)((data[pos] << 8) | data[pos + 1]);
                    raw[i] = (ushort)((firstPart * 256) | secondPart);
                }
            }

            //tables[tq] = new QuantizationTable
            //{
            //    Id = tq,
            //    Values = raw
            //};

            var natural = JpegDecoderHelpers.NaturalToZigzag(raw);
            ArrayPool<ushort>.Shared.Return(raw);

            tables[tq] = new QuantizationTable
            {
                Id = tq,
                Values = natural
            };
        }
    }
}
