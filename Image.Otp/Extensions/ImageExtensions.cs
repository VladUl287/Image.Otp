using Image.Otp.Primitives;
using System.Runtime.CompilerServices;
using Image.Otp.Models.Jpeg;
using Image.Otp.Parsers;
using System.Runtime.InteropServices;
using Image.Otp.Pixels;
using Image.Otp.Helpers;
using static Image.Otp.SixLabors.JpegMcuDecoder;
using Image.Otp.Enums;
using Image.Otp.Constants;
using System.Buffers;
using System;

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

                if (byteRead == JpegMarkers.OO)
                {
                    continue;
                }

                if (byteRead >= JpegMarkers.D0 && byteRead <= JpegMarkers.D7)
                {
                    continue;
                }

                if (JpegMarkers.HasLengthData(byteRead))
                {
                    var firstPart = stream.ReadByte();
                    var secondPart = stream.ReadByte();

                    //int length = (bytes[i + 2] << 8) | bytes[i + 3];
                    int length = (firstPart * 256) | secondPart;
                    length -= 2;

                    ProcessMarker(stream, byteRead, length, combiner);
                }
            }
        }

        return default;
    }

    private sealed class Combiner
    {
        public FrameInfo FrameInfo { get; set; }
        public ScanInfo ScanInfo { get; set; }

        public Dictionary<byte, QuantizationTable> QuantizationTables = [];

        public List<HuffmanTable> HuffmanTables = [];

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

        var data = new List<byte>();
        while (stream.CanRead)
        {
            var byteRead = stream.ReadByte();

            if (byteRead == JpegMarkers.FF)
            {
                byteRead = stream.ReadByte();

                if (byteRead == JpegMarkers.EOI)
                {
                    stream.Position -= 2;
                    break;
                }

                if (byteRead == JpegMarkers.OO)
                    continue;

                if (byteRead >= JpegMarkers.D0 && byteRead <= JpegMarkers.D7)
                    continue;

                break;
            }

            data.Add((byte)byteRead);
        }

        combiner.ScanInfo = new ScanInfo
        {
            Components = components,
            Ss = ss,
            Se = se,
            Ah = ah,
            Al = al,
            Data = [.. data]
        };
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
            var raw = new ushort[DqtTableSize];
            //var raw = ArrayPool<ushort>.Shared.Rent(DqtTableSize);

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

            tables[tq] = new QuantizationTable
            {
                Id = tq,
                Values = raw
            };

            //var natural = JpegDecoderHelpers.NaturalToZigzag(raw);
            //ArrayPool<ushort>.Shared.Return(raw);

            //tables[tq] = new QuantizationTable
            //{
            //    Id = tq,
            //    Values = natural
            //};
        }
    }

    private unsafe static Image<T> LoadJpeg<T>(byte[] bytes) where T : unmanaged, IPixel<T>
    {
        List<JpegSegment> segments = JpegParser.ParseSegmentsWithRestartMarkers(bytes);

        Dictionary<byte, QuantizationTable> qTables = JpegTableDecoder.ParseDqtSegments(segments);

        foreach (var kv in qTables)
        {
            Console.WriteLine($"DQT id={kv.Key}");
            var t = kv.Value;
            for (int i = 0; i < 64; i++)
            {
                Console.Write(t.Values[i] + (i % 8 == 7 ? "\n" : " "));
            }
        }

        List<HuffmanTable> hTables = JpegTableDecoder.ParseDhtSegments(segments);

        foreach (var ht in hTables.OrderBy(c => c.Class))
        {
            Console.WriteLine($"DHT class={ht.Class} id={ht.Id}");
            Console.Write("bits: ");
            for (int i = 1; i <= 16; i++) Console.Write(ht.CodeLengths[i - 1] + " ");
            Console.WriteLine();
            Console.Write("vals: ");
            foreach (var s in ht.Symbols) Console.Write(s.ToString("X2") + " ");
            Console.WriteLine();
        }

        JpegSegment frameSegment = segments.First(s => s.Marker == 0xC0 || s.Marker == 0xC2);
        var progressive = frameSegment.Marker == 0xC2;
        FrameInfo frameInfo = JpegTableDecoder.ParseSofSegment(frameSegment);

        var scanInfoss = JpegTableDecoder.ParseSosSegment(segments.First(s => s.Marker == 0xDA));

        for (int i = 0; i < frameInfo.Components.Length; i++)
        {
            var c = frameInfo.Components[i];
            var component = scanInfoss.Components.First(a => a.ComponentId == c.Id);
            Console.WriteLine($" comp {i}: id={c.Id} hsamp={c.HorizontalSampling} " +
                $"vsamp={c.VerticalSampling} quant={c.QuantizationTableId} dc_tbl={component.DcHuffmanTableId} ac_tbl={component.AcHuffmanTableId}");
        }

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
                qTables,
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

    // Alternative method using MemoryMarshal for better performance
    public static void FillFromByteArrayFast(this Image<Rgba32> image, byte[] rgbaData)
    {
        if (rgbaData.Length < image.Width * image.Height * 4)
        {
            throw new ArgumentException("Byte array is too small for the image dimensions");
        }

        var pixelSpan = MemoryMarshal.Cast<Rgba32, byte>(image.Pixels);
        rgbaData.CopyTo(pixelSpan);
        //rgbaData.AsSpan(0, pixelSpan.Length).CopyTo(pixelSpan);
    }

    // Method to create a new image from byte array
    public static Image<Rgba32> CreateFromByteArray(int width, int height, byte[] rgbaData)
    {
        var image = new Image<Rgba32>(width, height);
        image.FillFromByteArrayFast(rgbaData);
        return image;
    }
}
