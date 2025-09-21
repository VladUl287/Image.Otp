using Image.Otp.Constants;
using Image.Otp.Helpers;
using Image.Otp.Models.Jpeg;
using Image.Otp.Pixels;
using Image.Otp.Primitives;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics.X86;
using System.Runtime.Intrinsics;

namespace Image.Otp.Extensions;

public static class JpegExtensions
{
    public sealed class Accumulator
    {
        public FrameInfo FrameInfo { get; set; } = default!;

        public SOSSegment ScanInfo { get; set; } = default!;


        public Dictionary<byte, ushort[]> QuantTables { get; set; } = [];

        public List<HuffmanTable> HuffmanTables { get; set; } = [];

        public int RestartInterval { get; set; }
    }

    public unsafe static ImageOtp<T> LoadJpeg<T>(this Stream stream) where T : unmanaged, IPixel<T>
    {
        ImageOtp<T> image = default;

        var accumulator = new Accumulator();

        while (stream.CanRead)
        {
            var marker = stream.ReadByte();

            if (marker == JpegMarkers.FF)
            {
                marker = stream.ReadByte();

                if (marker == JpegMarkers.EOI)
                {
                    break;
                }

                if (JpegMarkers.HasLengthData(marker))
                {
                    var first = stream.ReadByte();
                    var second = stream.ReadByte();
                    var length = (first << 8) | second;
                    length -= 2;

                    switch (marker)
                    {
                        case JpegMarkers.DQT:
                            ProcessDQT(stream, stream.Position + length, accumulator.QuantTables);
                            break;
                        case JpegMarkers.SOF0:
                        case JpegMarkers.SOF2:
                            accumulator.FrameInfo = ProcessSOF(stream, stream.Position + length);
                            image = new ImageOtp<T>(accumulator.FrameInfo.Width, accumulator.FrameInfo.Height);
                            break;
                        case JpegMarkers.DHT:
                            ProcessDHT(stream, stream.Position + length, accumulator.HuffmanTables);
                            break;
                        case JpegMarkers.SOS:
                            accumulator.ScanInfo = ProcessSOS(stream, stream.Position + length, accumulator, image.Pixels);
                            break;
                        default:
                            stream.Seek(length, SeekOrigin.Current);
                            break;
                    }
                }
            }
        }

        return image;
    }

    private static void ProcessDQT(Stream stream, long endPosition, Dictionary<byte, ushort[]> qTables)
    {
        while (stream.Position < endPosition)
        {
            var pqTq = stream.ReadByte();

            var tq = (byte)(pqTq & 0x0F); // table id
            if (tq > 3)
                throw new InvalidDataException($"DQT table ID {tq} is invalid. Must be 0-3.");

            var pq = (pqTq >> 4) & 0x0F;   // precision: 0 = 8-bit, 1 = 16-bit
            //TODO: compare performace (pq != 0 && pq != 1)
            if ((uint)pq > 1u)
                throw new InvalidDataException($"Unsupported DQT precision {pq} in table {tq}.");

            const int SIZE = 64;

            var raw = new ushort[SIZE];
            if (pq == 0)
            {
                if (stream.Position + SIZE > endPosition)
                    throw new InvalidDataException("Truncated DQT segment for 8-bit table.");

                //Span<byte> buffer = stackalloc byte[SIZE * 2];
                //var bytesRead = stream.Read(buffer);
                //MemoryMarshal.Cast<byte, ushort>(buffer).CopyTo(raw);

                for (int i = 0; i < SIZE; i++)
                    raw[i] = (ushort)stream.ReadByte();
            }
            else
            {
                if (stream.Position + (SIZE * 2) > endPosition)
                    throw new InvalidDataException("Truncated DQT segment for 16-bit table.");

                for (int i = 0; i < SIZE; i++)
                    raw[i] = (ushort)stream.ReadBigEndianUInt16();
            }

            qTables[tq] = raw;

            if (stream.Position > endPosition)
                throw new InvalidDataException("Truncated DQT segment.");
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

    private static void ProcessDHT(Stream stream, long endPosition, List<HuffmanTable> tables)
    {
        if (stream.Position > endPosition)
            throw new InvalidDataException("Stream position exceeds segment boundary.");

        while (stream.Position < endPosition)
        {
            var tcTh = stream.ReadByte();
            if (tcTh == -1)
                throw new EndOfStreamException("Unexpected end of stream while reading DHT segment.");

            var tc = (byte)((tcTh >> 4) & 0x0F);
            var th = (byte)(tcTh & 0x0F);

            const int CODE_LENGTH = 16;
            var lengths = new byte[CODE_LENGTH];
            stream.ReadExactly(lengths, 0, CODE_LENGTH);

            int symbolCount = 0;
            for (int i = 0; i < CODE_LENGTH; i++)
                symbolCount += lengths[i];

            var symbols = new byte[symbolCount];
            stream.ReadExactly(symbols, 0, symbolCount);

            tables.Add(new HuffmanTable
            {
                Class = tc,
                Id = th,
                CodeLengths = lengths,
                Symbols = symbols
            });
        }
    }

    private static SOSSegment ProcessSOS<T>(Stream stream, long endPosition, Accumulator accumulator, Span<T> output) where T : unmanaged, IPixel<T>
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

        var huff = new Dictionary<(byte Class, byte Id), CanonicalHuffmanTable>(accumulator.HuffmanTables.Count);
        foreach (var ht in accumulator.HuffmanTables)
        {
            var table = HuffmanTableLogic.BuildCanonical(ht.CodeLengths, ht.Symbols);
            huff[(ht.Class, ht.Id)] = table;
        }

        accumulator.ScanInfo = new SOSSegment
        {
            Components = components,
            Ss = ss,
            Se = se,
            Ah = ah,
            Al = al
        };

        DecodeScanToBlocks(stream, huff, accumulator, output);

        return accumulator.ScanInfo;
    }

    private static void DecodeScanToBlocks<T>(
        Stream stream,
        Dictionary<(byte Class, byte Id), CanonicalHuffmanTable> huff,
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

        var compMap = frameInfo.Components.ToDictionary(c => c.Id);

        var scanComponents = scan.Components;

        int maxH = frameInfo.Components.Max(c => c.HorizontalSampling);
        int maxV = frameInfo.Components.Max(c => c.VerticalSampling);

        int mcuCols = (frameInfo.Width + (8 * maxH - 1)) / (8 * maxH);
        int mcuRows = (frameInfo.Height + (8 * maxV - 1)) / (8 * maxV);

        var dcPredictor = new Dictionary<byte, int>(scanComponents.Length);
        foreach (var sc in scanComponents) dcPredictor[sc.ComponentId] = 0;

        var bitReader = new StreamBitReader(stream);

        var componentBuffers = new Dictionary<byte, byte[]>();
        foreach (var comp in frameInfo.Components)
        {
            componentBuffers[comp.Id] = new byte[frameInfo.Width * frameInfo.Height];
            Array.Fill(componentBuffers[comp.Id], (byte)128);
        }

        int width = frameInfo.Width;
        int height = frameInfo.Height;

        for (int my = 0; my < mcuRows; my++)
        {
            for (int mx = 0; mx < mcuCols; mx++)
            {
                foreach (var sc in scanComponents)
                {
                    var comp = compMap[sc.ComponentId];

                    if (!acc.QuantTables.TryGetValue(comp.QuantizationTableId, out var qTable))
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

                            var blockStartX = mx * maxH * 8 + bx * 8 * scaleX;
                            var blockStartY = my * maxV * 8 + by * 8 * scaleY;

                            //UpsamplingScalarFallback(blockStartX, blockStartY, width, height, scaleX, scaleY, compBuffer, samples);
                            UpsamplingSimd(maxH, maxV, width, height, my, mx, compBuffer, scaleX, scaleY, by, bx, samples);
                        }
                    }
                }
            }
        }

        byte[] yBuffer = componentBuffers[1];
        byte[] cbBuffer = componentBuffers.ContainsKey(2) ? componentBuffers[2] : null;
        byte[] crBuffer = componentBuffers.ContainsKey(3) ? componentBuffers[3] : null;

        if (typeof(T) == typeof(Rgba32))
        {
            Span<Rgba32> rgbaOutput = MemoryMarshal.Cast<T, Rgba32>(output);

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

                rgbaOutput[i] = new Rgba32((byte)r, (byte)g, (byte)b);
            }
        }

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
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void UpsamplingSimd(int maxH, int maxV, int width, int height, int my, int mx,
        byte[] compBuffer, int scaleX, int scaleY, int by, int bx, double[] samples)
    {
        const int BlockSize = 8;

        var blockStartX = mx * maxH * BlockSize + bx * BlockSize * scaleX;
        var blockStartY = my * maxV * BlockSize + by * BlockSize * scaleY;

        if (Avx2.IsSupported && scaleX == 1 && scaleY == 1)
        {

        }

        if (Sse2.IsSupported && scaleX == 1 && scaleY == 1)
        {
            //UpsamplingSimdSse2(blockStartX, blockStartY, width, height, compBuffer, samples);
            //return;
        }

        UpsamplingScalarFallback(blockStartX, blockStartY, width, height, scaleX, scaleY, compBuffer, samples);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private unsafe static void UpsamplingSimdSse2(int blockStartX, int blockStartY, int width, int height, byte[] compBuffer, double[] samples)
    {
        const int BlockSize = 8;

        var startY = Math.Max(blockStartY, 0);
        var endY = Math.Min(blockStartY + BlockSize, height);
        var startX = Math.Max(blockStartX, 0);
        var endX = Math.Min(blockStartX + BlockSize, width);

        for (int sy = 0; sy < BlockSize; sy++)
        {
            int outY = blockStartY + sy;
            if (outY < 0 || outY >= height) continue;

            int rowOffset = outY * width;

            for (int sx = 0; sx < BlockSize; sx += 4)
            {
                // Convert 4 doubles to bytes
                var offset = Vector128.Create(128.0);
                var sampleVec1 = Vector128.Create(
                    samples[sy * BlockSize + sx + 0],
                    samples[sy * BlockSize + sx + 1]
                );
                var sampleVec2 = Vector128.Create(
                    samples[sy * BlockSize + sx + 2],
                    samples[sy * BlockSize + sx + 3]
                );

                var result1 = Sse2.Add(sampleVec1, offset);
                var result2 = Sse2.Add(sampleVec2, offset);

                var intResult1 = Sse2.ConvertToVector128Int32(result1);
                var intResult2 = Sse2.ConvertToVector128Int32(result2);

                var shortResult = Sse2.PackSignedSaturate(intResult1, intResult2);
                var byteResult = Sse2.PackUnsignedSaturate(shortResult, shortResult);

                byte* pixelValues = stackalloc byte[16];
                Sse2.Store(pixelValues, byteResult);

                for (int i = 0; i < 4; i++)
                {
                    int outX = blockStartX + sx + i;
                    if (outX < startX || outX >= endX) continue;

                    compBuffer[rowOffset + outX] = pixelValues[i];
                }
            }
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void UpsamplingScalarFallback(int blockStartX, int blockStartY, int width, int height, int scaleX, int scaleY, byte[] compBuffer, double[] samples)
    {
        const int BlockSize = 8;

        for (int sy = 0; sy < BlockSize; sy++)
        {
            for (int sx = 0; sx < BlockSize; sx++)
            {
                byte pixelValue = ConvertSampleToByte(samples[sy * BlockSize + sx]);

                int baseX = blockStartX + sx * scaleX;
                int baseY = blockStartY + sy * scaleY;

                FillScaledBlock(pixelValue, baseX, baseY, scaleX, scaleY, width, height, compBuffer);
            }
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static byte ConvertSampleToByte(double sample)
    {
        const double SampleOffset = 128.0;
        var value = (int)Math.Round(sample + SampleOffset);
        return (byte)Math.Clamp(value, 0, 255);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void FillScaledBlock(byte pixelValue, int baseX, int baseY,
        int scaleX, int scaleY, int width, int height, byte[] buffer)
    {
        var endY = Math.Min(baseY + scaleY, height);
        var endX = Math.Min(baseX + scaleX, width);

        var startY = Math.Max(baseY, 0);
        var startX = Math.Max(baseX, 0);

        for (var y = startY; y < endY; y++)
        {
            var rowOffset = y * width;
            for (var x = startX; x < endX; x++)
            {
                buffer[rowOffset + x] = pixelValue;
            }
        }
    }


    //public unsafe static ImageOtp<T> LoadJpeg<T>(this Stream stream) where T : unmanaged, IPixel<T>
    //{
    //    const int BufferSize = 4096;

    //    var buffer = ArrayPool<byte>.Shared.Rent(BufferSize);

    //    var image = new ImageOtp<T>(1, 1);

    //    fixed (byte* ptr = buffer)
    //    {
    //        var accumulator = new Accumulator();

    //        while (stream.Read(buffer, 0, buffer.Length) > 0)
    //        {
    //            var ffVector = Vector.Create(JpegMarkers.FF);

    //            for (int i = 0; i < buffer.Length; i += Vector<byte>.Count)
    //            {
    //                var data = Unsafe.ReadUnaligned<Vector<byte>>(ptr + i);

    //                var found = Vector.EqualsAny(data, ffVector);
    //                if (found)
    //                {
    //                    for (int j = i; j < buffer.Length; j++)
    //                    {
    //                        var marker = buffer[j];

    //                        if (marker == JpegMarkers.FF)
    //                        {
    //                            continue;
    //                        }

    //                        if (marker == JpegMarkers.EOI)
    //                        {
    //                            break;
    //                        }

    //                        if (JpegMarkers.HasLengthData(marker))
    //                        {
    //                            var first = buffer[++j];
    //                            var second = buffer[++j];
    //                            var length = (first << 8) | second;

    //                            if (j + length >= buffer.Length)
    //                            {
    //                                return image;
    //                                throw new NotSupportedException("Partial processing not supported yet.");
    //                            }

    //                            length -= 2;
    //                            j++;

    //                            var bufferSpan = buffer.AsSpan();
    //                            var segment = bufferSpan.Slice(j, length);
    //                            switch (marker)
    //                            {
    //                                case JpegMarkers.DQT:
    //                                    accumulator.QuantTables = ProcessDQT(segment);
    //                                    break;
    //                                case JpegMarkers.SOF0:
    //                                case JpegMarkers.SOF2:
    //                                    accumulator.FrameInfo = ProcessSOF(segment);
    //                                    break;
    //                            }

    //                            j += length;
    //                        }
    //                    }
    //                }
    //            }
    //        }
    //    }

    //    ArrayPool<byte>.Shared.Return(buffer);

    //    return image;
    //}

    //private static Dictionary<byte, ushort[]> ProcessDQT(ReadOnlySpan<byte> data)
    //{
    //    var i = 0;
    //    var dataLength = data.Length;
    //    var qtables = new Dictionary<byte, ushort[]>();
    //    while (i < dataLength)
    //    {
    //        if (i >= dataLength)
    //            throw new InvalidDataException("Truncated DQT segment.");

    //        var pqTq = data[i++];

    //        var tq = (byte)(pqTq & 0x0F); // table id
    //        if (tq > 3)
    //            throw new InvalidDataException($"DQT table ID {tq} is invalid. Must be 0-3.");

    //        var pq = (pqTq >> 4) & 0x0F;   // precision: 0 = 8-bit, 1 = 16-bit
    //        //TODO: compare performace
    //        //pq != 0 && pq != 1
    //        if ((uint)pq > 1u)
    //            throw new InvalidDataException($"Unsupported DQT precision {pq} in table {tq}.");

    //        const byte SIZE = 64;

    //        var raw = new ushort[SIZE];
    //        if (pq == 0)
    //        {
    //            if (i + SIZE > dataLength)
    //                throw new InvalidDataException("Truncated DQT segment for 8-bit table.");

    //            for (int j = 0; j < SIZE; j++)
    //                raw[j] = data[i++];
    //        }
    //        else
    //        {
    //            if (i + (SIZE * 2) > dataLength)
    //                throw new InvalidDataException("Truncated DQT segment for 16-bit table.");

    //            for (int j = 0; j < SIZE; j++)
    //            {
    //                raw[j] = (ushort)((data[i] << 8) | data[i + 1]);
    //                i += 2;
    //            }
    //        }

    //        qtables[tq] = raw;
    //    }

    //    if (i != dataLength)
    //        throw new InvalidDataException("DQT segment has unexpected trailing data.");

    //    return qtables;
    //}
}
