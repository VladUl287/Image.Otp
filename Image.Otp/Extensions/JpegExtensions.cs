using Image.Otp.Core.Constants;
using Image.Otp.Core.Helpers;
using Image.Otp.Core.Models.Jpeg;
using Image.Otp.Core.Pixels;
using System.Buffers;
using Image.Otp.Abstractions;
using System.Collections.Frozen;
using Image.Otp.Core.Helpers.Jpg;

namespace Image.Otp.Core.Extensions;

public static class JpegExtensions
{
    private sealed class Accumulator
    {
        public FrameInfo FrameInfo { get; set; } = default!;

        public SOSSegment ScanInfo { get; set; } = default!;

        public Dictionary<byte, float[]> QuantTables { get; init; } = [];

        public Dictionary<(byte Class, byte Id), CanonicalHuffmanTable> CanonicalHuffmanTables { get; init; } = [];

        public int RestartInterval { get; set; }
    }

    public unsafe static Image<T> LoadJpeg<T>(this Stream stream) where T : unmanaged, IPixel<T>
    {
        Image<T> image = default;

        var accumulator = new Accumulator();

        DefaultArrayPool<float> pool = default;

        while (stream.Position < stream.Length)
        {
            var marker = stream.ReadByte();

            if (marker == JpegMarkers.FF)
            {
                marker = stream.ReadByte();

                if (marker == JpegMarkers.EOI)
                    break;

                if (JpegMarkers.HasLengthData(marker))
                {
                    var first = stream.ReadByte();
                    var second = stream.ReadByte();
                    var length = (first << 8) | second;
                    length -= 2;

                    switch (marker)
                    {
                        case JpegMarkers.DQT:
                            ProcessDQT(stream, stream.Position + length, accumulator.QuantTables, pool);
                            break;
                        case JpegMarkers.SOF0:
                        case JpegMarkers.SOF2:
                            accumulator.FrameInfo = ProcessSOF(stream, stream.Position + length);
                            image = new Image<T>(accumulator.FrameInfo.Width, accumulator.FrameInfo.Height);
                            break;
                        case JpegMarkers.DRI:
                            accumulator.RestartInterval = stream.ReadBigEndianUInt16();
                            break;
                        case JpegMarkers.DHT:
                            ProcessDHT(stream, stream.Position + length, accumulator.CanonicalHuffmanTables);
                            break;
                        case JpegMarkers.SOS:
                            ProcessSOS(stream, stream.Position + length, accumulator, image.Pixels);
                            break;
                        default:
                            stream.Seek(length, SeekOrigin.Current);
                            break;
                    }
                }
            }
        }

        foreach (var qTable in accumulator.QuantTables.Values)
            pool.Return(qTable);

        return image;
    }

    private static void ProcessDQT<TAllocator>(Stream stream, long endPosition, Dictionary<byte, float[]> qTables, TAllocator allocator)
        where TAllocator : notnull, IArrayPool<float>
    {
        const int BLOCK_SIZE = 64;
        const int MAX_BUFFER_SIZE = 128;

        Span<byte> buffer = stackalloc byte[MAX_BUFFER_SIZE];

        while (stream.Position < endPosition)
        {
            var pqTq = stream.ReadByte();

            var tq = (byte)(pqTq & 0x0F); // table id
            if (tq > 3)
                throw new InvalidDataException($"DQT table ID {tq} is invalid. Must be 0-3.");

            var pq = (pqTq >> 4) & 0x0F;   // precision: 0 = 8-bit, 1 = 16-bit
            if (pq != 0 && pq != 1)
                throw new InvalidDataException($"Unsupported DQT precision {pq} in table {tq}.");

            var raw = allocator.Rent(BLOCK_SIZE);
            if (pq == 0)
            {
                if (stream.Position + BLOCK_SIZE > endPosition)
                    throw new InvalidDataException("Truncated DQT segment for 8-bit table.");

                Span<byte> byteBuffer = buffer[..BLOCK_SIZE];
                stream.ReadExactly(byteBuffer);

                for (int i = 0; i < BLOCK_SIZE; i++)
                    raw[i] = byteBuffer[i];
            }
            else
            {
                if (stream.Position + MAX_BUFFER_SIZE > endPosition)
                    throw new InvalidDataException("Truncated DQT segment for 16-bit table.");

                stream.ReadExactly(buffer);

                for (int i = 0; i < BLOCK_SIZE; i++)
                {
                    var offset = i * 2;
                    raw[i] = (buffer[offset] << 8) | buffer[offset + 1];
                }
            }

            qTables[tq] = raw;
        }

        if (stream.Position != endPosition)
            throw new InvalidDataException("DQT segment has unexpected trailing data.");
    }

    private static FrameInfo ProcessSOF(Stream stream, long endPosition)
    {
        const int MIN_LENGTH = 6; // Precision(1) + Height(2) + Width(2) + NumComponents(1)
        const int BYTES_PER_COMPONENT = 3; // ID(1) + SamplingFactor(1) + QuantTableId(1)

        if (stream.Position > endPosition)
            throw new InvalidDataException("Stream position exceeds segment boundary.");

        var length = endPosition - stream.Position;
        if (length < MIN_LENGTH)
            throw new InvalidDataException($"SOF segment too short. Expected at least {MIN_LENGTH} bytes, got {length}.");

        var precision = (byte)stream.ReadByte(); // Read precision (usually 8)

        var height = stream.ReadBigEndianUInt16();
        if (height <= 0)
            throw new InvalidDataException($"Invalid height: {height}. Must be positive.");

        int width = stream.ReadBigEndianUInt16();
        if (width <= 0)
            throw new InvalidDataException($"Invalid width: {width}. Must be positive.");

        var numComponents = stream.ReadByte();
        if (numComponents is < 1 or > 4)
            throw new InvalidDataException($"Invalid number of components: {numComponents}. Must be between 1 and 4.");

        var expectedLength = MIN_LENGTH + numComponents * BYTES_PER_COMPONENT;
        if (length < expectedLength)
            throw new InvalidDataException("SOF segment too short for components.");

        var components = new ComponentInfo[numComponents];
        for (var i = 0; i < numComponents; i++)
        {
            components[i] = new ComponentInfo
            {
                Id = (byte)stream.ReadByte(),
                SamplingFactor = (byte)stream.ReadByte(),
                QuantizationTableId = (byte)stream.ReadByte()
            };
        }

        return new FrameInfo
        {
            Precision = precision,
            Width = width,
            Height = height,
            Components = components
        };
    }

    private static void ProcessDHT(Stream stream, long endPosition, Dictionary<(byte Class, byte Id), CanonicalHuffmanTable> tables)
    {
        if (stream.Position > endPosition)
            throw new InvalidDataException("Stream position exceeds segment boundary.");

        const int CODE_LENGTH = 16;

        Span<byte> lengths = stackalloc byte[CODE_LENGTH];

        while (stream.Position < endPosition)
        {
            var tcTh = stream.ReadByte();
            if (tcTh == -1)
                throw new EndOfStreamException("Unexpected end of stream while reading DHT segment.");

            var tc = (byte)((tcTh >> 4) & 0x0F);
            var th = (byte)(tcTh & 0x0F);

            stream.ReadExactly(lengths);

            int symbolCount = 0;
            for (int i = 0; i < CODE_LENGTH; i++)
                symbolCount += lengths[i];

            Span<byte> symbols = stackalloc byte[symbolCount];
            stream.ReadExactly(symbols);

            var table = HuffmanTableLogic.BuildCanonical(lengths, symbols);
            tables[(tc, th)] = table;
        }
    }

    private static void ProcessSOS<T>(Stream stream, long endPosition, Accumulator accumulator, Span<T> output) where T : unmanaged, IPixel<T>
    {
        const int MIN_LENGTH = 6;
        const int NUM_COMPONENTS = 1; //1 byte (numComponents)
        const int BYTES_PER_COMPONENT = 2;
        const int SPECTRAL_LENGTH = 3; //3 bytes (Ss, Se, AhAl)

        if (stream.Position > endPosition)
            throw new InvalidDataException("Stream position exceeds segment boundary.");

        var length = endPosition - stream.Position;
        if (length < MIN_LENGTH)
            throw new InvalidDataException($"SOS segment too short. Expected at least {MIN_LENGTH} bytes, got {length}.");

        var numComponents = stream.ReadByte();

        if (numComponents is < 1 or > 4)
            throw new InvalidDataException($"Invalid number of components: {numComponents}. Must be between 1 and 4.");

        var requredLength = NUM_COMPONENTS + (BYTES_PER_COMPONENT * numComponents) + SPECTRAL_LENGTH;
        if (length != requredLength)
            throw new InvalidDataException("SOS segment length mismatch.");

        var components = new ScanComponent[numComponents];
        for (int i = 0; i < numComponents; i++)
        {
            var componentId = (byte)stream.ReadByte();
            var huffmanTableIds = (byte)stream.ReadByte();
            components[i] = new ScanComponent
            {
                ComponentId = componentId,
                DcHuffmanTableId = (byte)(huffmanTableIds >> 4),
                AcHuffmanTableId = (byte)(huffmanTableIds & 0x0F)
            };
        }

        byte ss = (byte)stream.ReadByte();
        byte se = (byte)stream.ReadByte();
        byte ahAl = (byte)stream.ReadByte();
        byte ah = (byte)(ahAl >> 4);
        byte al = (byte)(ahAl & 0x0F);

        accumulator.ScanInfo = new SOSSegment
        {
            Components = components,
            Ss = ss,
            Se = se,
            Ah = ah,
            Al = al
        };

        DecodeScanToBlocks(stream, accumulator, output);
    }

    private unsafe static void DecodeScanToBlocks<T>(
        Stream stream,
        Accumulator acc,
        Span<T> output) where T : unmanaged, IPixel<T>
    {
        ArgumentNullException.ThrowIfNull(stream);
        ArgumentNullException.ThrowIfNull(acc);
        ArgumentNullException.ThrowIfNull(acc.FrameInfo);
        ArgumentNullException.ThrowIfNull(acc.ScanInfo);

        var frameInfo = acc.FrameInfo;
        var scan = acc.ScanInfo;
        var restartInterval = acc.RestartInterval;

        if (scan.Ss != 0 || scan.Se != 63 || scan.Ah != 0 || scan.Al != 0)
            throw new ArgumentException("This decoder only supports baseline non-progressive scans (Ss=0,Se=63,Ah=0,Al=0).");

        var sofComponents = frameInfo.Components.ToDictionary(c => c.Id);
        var sosComponents = scan.Components.AsSpan();

        var maxH = frameInfo.Components.Max(c => c.HorizontalSampling);
        var maxV = frameInfo.Components.Max(c => c.VerticalSampling);

        var mcuCols = (frameInfo.Width + (8 * maxH - 1)) / (8 * maxH);
        var mcuRows = (frameInfo.Height + (8 * maxV - 1)) / (8 * maxV);

        var dcPredictor = new Dictionary<byte, int>(sosComponents.Length);
        foreach (var sc in sosComponents)
            dcPredictor[sc.ComponentId] = 0;

        var componentBuffers = new Dictionary<byte, byte[]>(frameInfo.Components.Length);
        foreach (var comp in frameInfo.Components)
        {
            componentBuffers[comp.Id] = ArrayPool<byte>.Shared.Rent(frameInfo.Width * frameInfo.Height);
            componentBuffers[comp.Id].AsSpan().Fill(128);
        }

        var huff = acc.CanonicalHuffmanTables;
        var qTables = acc.QuantTables;

        var bitReader = new StreamBitReader(stream);

        var width = frameInfo.Width;
        var height = frameInfo.Height;

        Span<float> block = stackalloc float[64];
        for (var my = 0; my < mcuRows; my++)
        {
            for (var mx = 0; mx < mcuCols; mx++)
            {
                foreach (var sc in sosComponents)
                {
                    var comp = sofComponents[sc.ComponentId];
                    var qTable = qTables[comp.QuantizationTableId]
                        ?? throw new InvalidOperationException($"Quantization table {comp.QuantizationTableId} not found.");

                    var buffer = componentBuffers[comp.Id];

                    var dcTable = huff[(0, sc.DcHuffmanTableId)];
                    var acTable = huff[(1, sc.AcHuffmanTableId)];

                    var h = comp.HorizontalSampling;
                    var v = comp.VerticalSampling;
                    var scaleX = maxH / h;
                    var scaleY = maxV / v;

                    for (int by = 0; by < v; by++)
                    {
                        for (int bx = 0; bx < h; bx++)
                        {
                            block[0] = GetDc(dcPredictor, bitReader, sc, dcTable);
                            SetAc(bitReader, acTable, block);
                            
                            const int BLOCK_SIZE = 8;
                            var blockStartX = mx * maxH * BLOCK_SIZE + bx * BLOCK_SIZE * scaleX;
                            var blockStartY = my * maxV * BLOCK_SIZE + by * BLOCK_SIZE * scaleY;

                            block
                                .DequantizeInPlace(qTable)
                                .ZigZagToNaturalInPlace()
                                .IDCT8x8InPlace()
                                .UpsampleInPlace(buffer, width, height, scaleX, scaleY, blockStartX, blockStartY)
                                ;
                        }
                    }

                    block.Clear();
                }
            }
        }

        byte[] yBuffer = componentBuffers[1];
        componentBuffers.TryGetValue(2, out var cbBuffer);
        componentBuffers.TryGetValue(3, out var crBuffer);

        var processor = PixelProcessorFactory.GetProcessor<T>();

        fixed (byte* yPtr = yBuffer)
        fixed (byte* cbPtr = cbBuffer)
        fixed (byte* crPtr = crBuffer)
        {
            processor.FromYCbCr(yPtr, cbPtr, crPtr, output);
        }

        ArrayPool<byte>.Shared.Return(yBuffer, true);
        if (cbBuffer is not null) ArrayPool<byte>.Shared.Return(cbBuffer, true);
        if (crBuffer is not null) ArrayPool<byte>.Shared.Return(crBuffer, true);
    }

    static int GetDc(Dictionary<byte, int> dcPredictor, StreamBitReader bitReader, ScanComponent sc, CanonicalHuffmanTable dcTable)
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

            dcDiff = JpegBlockProcessor.ExtendSign(bits, magnitude);
        }

        var prevDc = dcPredictor[sc.ComponentId];
        var dcVal = prevDc + dcDiff;
        dcPredictor[sc.ComponentId] = dcVal;

        return dcVal;
    }

    static void SetAc(StreamBitReader bitReader, CanonicalHuffmanTable acTable, Span<float> block)
    {
        int k = 1;
        while (k < 64)
        {
            int acSym = JpegHelpres.DecodeHuffmanSymbol(bitReader, acTable);
            if (acSym < 0)
                throw new EndOfStreamException("Marker or EOF encountered while decoding AC.");

            if (acSym == 0x00)
                break;

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

            var level = JpegBlockProcessor.ExtendSign(bits, size);
            block[k] = level;
            k++;
        }
    }
}
