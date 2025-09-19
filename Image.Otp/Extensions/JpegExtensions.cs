using Image.Otp.Constants;
using Image.Otp.Models.Jpeg;
using Image.Otp.Pixels;
using System.Buffers;
using System.Numerics;
using System.Runtime.CompilerServices;

namespace Image.Otp.Extensions;

public static class JpegExtensions
{
    public sealed class Accumulator
    {
        public FrameInfo FrameInfo { get; set; } = default!;

        public ScanInfo ScanInfo { get; set; } = default!;

        public Dictionary<byte, ushort[]> QuantTables { get; set; } = default!;

        public List<HuffmanTable> HuffmanTables { get; set; } = [];

        public int RestartInterval { get; set; }
    }

    public unsafe static ImageOtp<T> LoadJpeg<T>(this Stream stream) where T : unmanaged, IPixel<T>
    {
        const int BufferSize = 4096;

        var buffer = ArrayPool<byte>.Shared.Rent(BufferSize);

        var image = new ImageOtp<T>(1, 1);

        fixed (byte* ptr = buffer)
        {
            var accumulator = new Accumulator();

            while (stream.Read(buffer, 0, buffer.Length) > 0)
            {
                var ffVector = Vector.Create(JpegMarkers.FF);

                for (int i = 0; i < buffer.Length; i += Vector<byte>.Count)
                {
                    var data = Unsafe.ReadUnaligned<Vector<byte>>(ptr + i);

                    var found = Vector.EqualsAny(data, ffVector);
                    if (found)
                    {
                        for (int j = i; j < buffer.Length; j++)
                        {
                            var marker = buffer[j];

                            if (marker == JpegMarkers.FF)
                            {
                                continue;
                            }

                            if (marker == JpegMarkers.EOI)
                            {
                                break;
                            }

                            if (JpegMarkers.HasLengthData(marker))
                            {
                                var first = buffer[++j];
                                var second = buffer[++j];
                                var length = (first << 8) | second;

                                if (j + length >= buffer.Length)
                                {
                                    return image;
                                    throw new NotSupportedException("Partial processing not supported yet.");
                                }

                                length -= 2;
                                j++;

                                var bufferSpan = buffer.AsSpan();
                                var segment = bufferSpan.Slice(j, length);
                                switch (marker)
                                {
                                    case JpegMarkers.DQT:
                                        accumulator.QuantTables = ProcessDQT(segment);
                                        break;
                                    case JpegMarkers.SOF0:
                                    case JpegMarkers.SOF2:
                                        accumulator.FrameInfo = ProcessSOF(segment);
                                        break;
                                }

                                j += length;
                            }
                        }
                    }
                }
            }
        }

        ArrayPool<byte>.Shared.Return(buffer);

        return image;
    }

    private static FrameInfo ProcessSOF(ReadOnlySpan<byte> data)
    {
        if (data.Length < 6)
            throw new InvalidDataException("SOF segment too short.");

        //var precision = (byte)stream.ReadByte(); // Read precision (usually 8)

        //var firstPart = stream.ReadByte();
        //var secondPart = stream.ReadByte();
        ////TODO: int height = (seg.Data[pos++] << 8) | seg.Data[pos++];
        //int height = (firstPart * 256) | secondPart;

        //firstPart = stream.ReadByte();
        //secondPart = stream.ReadByte();
        ////TODO: int width = (seg.Data[pos++] << 8) | seg.Data[pos++];
        //int width = (firstPart * 256) | secondPart;

        //var numComponents = stream.ReadByte();
        ////TODO: Check if there's enough data for all components
        //if (length < 6 + 3 * numComponents)
        //    throw new InvalidDataException("SOF segment too short for components.");

        //var components = new ComponentInfo[numComponents];
        //for (int i = 0; i < numComponents; i++)
        //{
        //    var id = (byte)stream.ReadByte();
        //    var samplingFactor = (byte)stream.ReadByte();
        //    components[i] = new ComponentInfo
        //    {
        //        Id = id,
        //        SamplingFactor = samplingFactor,
        //        HorizontalSampling = (byte)(samplingFactor >> 4),   // Upper 4 bits
        //        VerticalSampling = (byte)(samplingFactor & 0x0F),   // Lower 4 bits
        //        QuantizationTableId = (byte)stream.ReadByte()
        //    };
        //}

        return new FrameInfo
        {
            //Precision = precision, // Include precision in output
            //Width = width,
            //Height = height,
            //Components = components
        };
    }

    private static Dictionary<byte, ushort[]> ProcessDQT(ReadOnlySpan<byte> data)
    {
        var i = 0;
        var dataLength = data.Length;
        var qtables = new Dictionary<byte, ushort[]>();
        while (i < dataLength)
        {
            if (i >= dataLength)
                throw new InvalidDataException("Truncated DQT segment.");

            var pqTq = data[i++];

            var tq = (byte)(pqTq & 0x0F); // table id
            if (tq > 3)
                throw new InvalidDataException($"DQT table ID {tq} is invalid. Must be 0-3.");

            var pq = (pqTq >> 4) & 0x0F;   // precision: 0 = 8-bit, 1 = 16-bit
            //TODO: compare performace
            //pq != 0 && pq != 1
            if ((uint)pq > 1u)
                throw new InvalidDataException($"Unsupported DQT precision {pq} in table {tq}.");

            const byte SIZE = 64;

            var raw = new ushort[SIZE];
            if (pq == 0)
            {
                if (i + SIZE > dataLength)
                    throw new InvalidDataException("Truncated DQT segment for 8-bit table.");

                for (int j = 0; j < SIZE; j++)
                    raw[j] = data[i++];
            }
            else
            {
                if (i + (SIZE * 2) > dataLength)
                    throw new InvalidDataException("Truncated DQT segment for 16-bit table.");

                for (int j = 0; j < SIZE; j++)
                {
                    raw[j] = (ushort)((data[i] << 8) | data[i + 1]);
                    i += 2;
                }
            }

            qtables[tq] = raw;
        }

        if (i != dataLength)
            throw new InvalidDataException("DQT segment has unexpected trailing data.");

        return qtables;
    }
}
