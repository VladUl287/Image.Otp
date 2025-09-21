using Image.Otp.Constants;
using Image.Otp.Models.Jpeg;
using Image.Otp.Pixels;

namespace Image.Otp.Extensions;

public static class JpegExtensions
{
    public sealed class Accumulator
    {
        public FrameInfo FrameInfo { get; set; } = default!;

        public ScanInfo ScanInfo { get; set; } = default!;

        public Dictionary<byte, ushort[]> QuantTables { get; set; } = [];

        public List<HuffmanTable> HuffmanTables { get; set; } = [];

        public int RestartInterval { get; set; }
    }

    public unsafe static ImageOtp<T> LoadJpeg<T>(this Stream stream) where T : unmanaged, IPixel<T>
    {
        var image = new ImageOtp<T>(1, 1);

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
                            break;
                        case JpegMarkers.DHT:
                            ProcessDHT(stream, stream.Position + length, accumulator.HuffmanTables);
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
