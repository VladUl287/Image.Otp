using Image.Otp.Models.Jpeg;

namespace Image.Otp.Parsers;

public static class JpegTableDecoder
{
    public static ScanInfo ParseSosSegment(JpegSegment seg)
    {
        if (seg.Marker != 0xDA)
            throw new InvalidDataException("Invalid SOS marker.");

        if (seg.Data.Length < 6)
            throw new InvalidDataException("SOS segment too short.");

        int pos = 0;
        byte numComponents = seg.Data[pos++];

        // Validate component count
        if (numComponents < 1 || numComponents > 4)
            throw new InvalidDataException($"Invalid number of components: {numComponents}");

        // Validate segment length: 1 byte (numComponents) + 2*numComponents + 3 bytes (Ss, Se, AhAl)
        if (seg.Data.Length != 1 + 2 * numComponents + 3)
            throw new InvalidDataException("SOS segment length mismatch.");

        var components = new List<ScanComponent>();
        for (int i = 0; i < numComponents; i++)
        {
            if (pos + 1 >= seg.Data.Length)
                throw new InvalidDataException("Unexpected end of SOS segment.");

            components.Add(new ScanComponent
            {
                ComponentId = seg.Data[pos++],
                DcHuffmanTableId = (byte)(seg.Data[pos] >> 4),
                AcHuffmanTableId = (byte)(seg.Data[pos] & 0x0F)
            });
            pos++;
        }

        // Read spectral parameters
        byte ss = seg.Data[pos++];
        byte se = seg.Data[pos++];
        byte ahAl = seg.Data[pos++];
        byte ah = (byte)(ahAl >> 4);
        byte al = (byte)(ahAl & 0x0F);

        // Validate spectral parameters for baseline JPEG
        //if (ss != 0 || se != 63 || ah != 0 || al != 0)
        //    throw new InvalidDataException("Spectral parameters do not match baseline JPEG.");

        return new ScanInfo
        {
            Components = components,
            Ss = ss,
            Se = se,
            Ah = ah,
            Al = al
        };
    }

    public static FrameInfo ParseSofSegment(JpegSegment seg)
    {
        // Expand valid SOF markers to include all standard SOF markers
        if (seg.Marker < 0xC0 || seg.Marker > 0xCF || seg.Marker == 0xC4 || seg.Marker == 0xC8 || seg.Marker == 0xCC)
            throw new InvalidDataException("Invalid SOF marker.");

        // Verify minimum length: precision(1) + dimensions(4) + numComponents(1) + components*3
        if (seg.Data.Length < 6)
            throw new InvalidDataException("SOF segment too short.");

        int pos = 0;
        byte precision = seg.Data[pos++]; // Read precision (usually 8)

        int height = (seg.Data[pos++] << 8) | seg.Data[pos++];
        int width = (seg.Data[pos++] << 8) | seg.Data[pos++];

        byte numComponents = seg.Data[pos++];
        // Check if there's enough data for all components
        if (seg.Data.Length < 6 + 3 * numComponents)
            throw new InvalidDataException("SOF segment too short for components.");

        var components = new List<ComponentInfo>(numComponents);
        for (int i = 0; i < numComponents; i++)
        {
            components.Add(new ComponentInfo
            {
                Id = seg.Data[pos],
                SamplingFactor = seg.Data[pos + 1],
                HorizontalSampling = (byte)(seg.Data[pos + 1] >> 4),   // Upper 4 bits
                VerticalSampling = (byte)(seg.Data[pos + 1] & 0x0F),   // Lower 4 bits
                QuantizationTableId = seg.Data[pos + 2]
            });
            pos += 3;
        }

        return new FrameInfo
        {
            Precision = precision, // Include precision in output
            Width = width,
            Height = height,
            Components = components.ToArray()
        };
    }

    public static List<HuffmanTable> ParseDhtSegments(List<JpegSegment> segments)
    {
        var tables = new List<HuffmanTable>();

        foreach (var seg in segments)
        {
            if (seg.Marker != 0xC4) continue; // DHT marker

            int pos = 0;
            byte[] data = seg.Data;

            while (pos < data.Length)
            {
                byte tcTh = data[pos++];
                byte tc = (byte)((tcTh >> 4) & 0x0F);
                byte th = (byte)(tcTh & 0x0F);

                // Read 16 code lengths
                byte[] lengths = new byte[16];
                Buffer.BlockCopy(data, pos, lengths, 0, 16);
                pos += 16;

                // Calculate total symbols
                int symbolCount = 0;
                for (int i = 0; i < 16; i++)
                    symbolCount += lengths[i];

                // Read symbols
                byte[] symbols = new byte[symbolCount];
                Buffer.BlockCopy(data, pos, symbols, 0, symbolCount);
                pos += symbolCount;

                tables.Add(new HuffmanTable
                {
                    Class = tc,
                    Id = th,
                    CodeLengths = lengths,
                    Symbols = symbols
                });
            }
        }

        return tables;
    }

    //public static List<HuffmanTable> ParseDhtSegments(List<JpegSegment> segments)
    //{
    //    var tables = new List<HuffmanTable>();

    //    var index = 0;
    //    foreach (var seg in segments)
    //    {
    //        if (seg.Marker != 0xC4) // DHT
    //            continue;

    //        int pos = 0;
    //        byte[] data = seg.Data;

    //        while (pos < data.Length)
    //        {
    //            // Check if there's enough data for tcTh + 16 code lengths
    //            if (pos + 17 > data.Length)
    //                throw new InvalidDataException("DHT segment truncated at table header.");

    //            byte tcTh = data[pos++];
    //            byte tc = (byte)((tcTh >> 4) & 0x0F);
    //            byte th = (byte)(tcTh & 0x0F);

    //            if (tc > 1) throw new InvalidDataException($"Unsupported Huffman table class {tc}.");
    //            if (th > 3) throw new InvalidDataException($"Unsupported Huffman table id {th}.");

    //            // Read 16 code lengths
    //            byte[] lengths = new byte[16];
    //            for (int i = 0; i < 16; i++)
    //                lengths[i] = data[pos++];

    //            // Calculate total symbols
    //            int symbolCount = 0;
    //            for (int i = 0; i < 16; i++)
    //                symbolCount += lengths[i];

    //            // Check if symbols data is available
    //            if (pos + symbolCount > data.Length)
    //                throw new InvalidDataException("DHT segment truncated in symbols data.");

    //            byte[] symbols = new byte[symbolCount];
    //            Array.Copy(data, pos, symbols, 0, symbolCount);
    //            pos += symbolCount;

    //            tables.Add(new HuffmanTable
    //            {
    //                Class = tc,
    //                Id = th,
    //                CodeLengths = lengths,
    //                Symbols = symbols,
    //            });

    //            index++;
    //        }
    //    }

    //    return tables;
    //}

    public static Dictionary<byte, QuantizationTable> ParseDqtSegments(List<JpegSegment> segments)
    {
        var tables = new Dictionary<byte, QuantizationTable>();

        foreach (var seg in segments)
        {
            if (seg.Marker != 0xDB) // DQT
                continue;

            int pos = 0;
            byte[] data = seg.Data;
            while (pos < data.Length)
            {
                if (pos >= data.Length)
                    throw new InvalidDataException("Truncated DQT segment.");

                byte pqTq = data[pos++];
                int pq = (pqTq >> 4) & 0x0F;   // precision: 0 = 8-bit, 1 = 16-bit
                byte tq = (byte)(pqTq & 0x0F); // table id

                // Validate table ID
                if (tq > 3)
                    throw new InvalidDataException($"DQT table ID {tq} is invalid. Must be 0-3.");

                if (pq != 0 && pq != 1)
                    throw new InvalidDataException($"Unsupported DQT precision {pq} in table {tq}.");

                const int valueCount = 64;
                var raw = new ushort[valueCount];

                // read raw (zig-zag order)
                if (pq == 0)
                {
                    for (int i = 0; i < valueCount; i++)
                        raw[i] = data[pos++];
                }
                else
                {
                    for (int i = 0; i < valueCount; i++)
                    {
                        raw[i] = (ushort)((data[pos] << 8) | data[pos + 1]);
                        pos += 2;
                    }
                }

                // normalize to natural (row-major) order
                //var natural = new ushort[valueCount];
                //for (int i = 0; i < valueCount; i++)
                //{
                //    natural[ZigZag[i]] = raw[i]; // wire index i -> natural index ZigZag[i]
                //}
                var natural = JpegDecoderHelpers.NaturalToZigzag(raw);

                tables[tq] = new QuantizationTable
                {
                    Id = tq,
                    Values = natural
                };
            }

            // Optional: Check if entire segment was consumed
            if (pos != data.Length)
                throw new InvalidDataException("DQT segment has unexpected trailing data.");
        }

        return tables;
    }
}
