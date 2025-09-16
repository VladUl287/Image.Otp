namespace Image.Otp.Models.Jpeg;

public sealed class CanonicalHuffmanTable
{
    public Guid Id { get; init; } = Guid.NewGuid();

    public readonly Dictionary<int, byte>[] _byLength; // index 1..16

    public CanonicalHuffmanTable()
    {
        _byLength = new Dictionary<int, byte>[17];
        for (int i = 0; i <= 16; i++) _byLength[i] = new Dictionary<int, byte>();
    }

    public void Add(int code, int length, byte symbol)
    {
        if (length <= 0 || length > 16) throw new ArgumentOutOfRangeException(nameof(length));
        _byLength[length][code] = symbol;
    }

    public bool TryGetSymbol(int code, int length, out byte symbol)
    {
        symbol = 0;
        if (length <= 0 || length > 16) return false;
        return _byLength[length].TryGetValue(code, out symbol);
    }
}

public sealed class HuffmanTableLogic
{
    //    public static CanonicalHuffmanTable BuildCanonical(byte[] bits, byte[] symbols)
    //    {
    //        if (bits == null) throw new ArgumentNullException(nameof(bits));
    //        if (symbols == null) throw new ArgumentNullException(nameof(symbols));
    //        if (bits.Length != 16) throw new ArgumentException("bits must have exactly 16 entries.", nameof(bits));

    //        var table = new CanonicalHuffmanTable();
    //        int symbolIndex = 0;

    //        // Calculate starting codes for each length
    //        uint[] codes = new uint[17]; // codes[0] unused, codes[1] = first code for length 1, etc.
    //        codes[1] = 0;

    //        for (int i = 2; i <= 16; i++)
    //        {
    //            codes[i] = (uint)((codes[i - 1] + bits[i - 2]) << 1);
    //        }

    //        // Assign codes to symbols
    //        for (int bitLength = 1; bitLength <= 16; bitLength++)
    //        {
    //            for (int i = 0; i < bits[bitLength - 1]; i++)
    //            {
    //                if (symbolIndex >= symbols.Length)
    //                    throw new ArgumentException("symbols array ended prematurely.", nameof(symbols));

    //                var symbol = symbols[symbolIndex++];
    //                table.Add((int)codes[bitLength], bitLength, symbol);

    //                //Console.WriteLine($"id = {table.Id} sym={symbol} code={Convert.ToString(codes[bitLength], 2).PadLeft(bitLength, '0')} len={bitLength}");

    //                codes[bitLength]++;
    //            }
    //        }

    //        return table;
    //    }

    //public static CanonicalHuffmanTable BuildCanonical(byte[] lengths, byte[] symbols)
    //{
    //    if (lengths == null) throw new ArgumentNullException(nameof(lengths));
    //    if (symbols == null) throw new ArgumentNullException(nameof(symbols));
    //    if (lengths.Length < 16) throw new ArgumentException("lengths must have at least 16 entries (for lengths 1..16).", nameof(lengths));

    //    var table = new CanonicalHuffmanTable();

    //    // JPEG lengths array: counts for lengths 1..16 stored in lengths[0..15]
    //    // Validate total symbols count does not exceed provided symbols array
    //    int total = 0;
    //    for (int i = 0; i < 16; i++)
    //    {
    //        total += lengths[i];
    //    }
    //    if (total > symbols.Length) throw new ArgumentException("symbols array too short for lengths counts.", nameof(symbols));

    //    int symbolIndex = 0;
    //    int code = 0;

    //    // For each bit length from 1 to 16
    //    for (int bitLength = 1; bitLength <= 16; bitLength++)
    //    {
    //        int count = lengths[bitLength - 1];
    //        if (count == 0)
    //        {
    //            // Shift code for next length (equivalent to left shift by 1)
    //            code <<= 1;
    //            continue;
    //        }

    //        // When increasing length, the canonical code is left-shifted by 1 from previous length.
    //        code <<= 1;

    //        for (int i = 0; i < count; i++)
    //        {
    //            if (symbolIndex >= symbols.Length) throw new ArgumentException("symbols array ended prematurely.", nameof(symbols));

    //            // In JPEG, codes are assigned in increasing order. The code variable here is the next code for this length.
    //            // However the representation used for lookup in many decoders uses the MSB-first bit ordering.
    //            // We will store the code value as-is (the canonical integer code).
    //            table.Add(code, bitLength, symbols[symbolIndex++]);

    //            code++;
    //        }
    //    }

    //    return table;
    //}

    public static CanonicalHuffmanTable BuildCanonical(byte[] lengths, byte[] symbols)
    {
        if (lengths == null || lengths.Length != 16)
            throw new ArgumentException("Lengths array must have exactly 16 elements", nameof(lengths));

        symbols ??= Array.Empty<byte>();

        // Calculate total number of codes
        int totalCodes = 0;
        for (int i = 0; i < 16; i++)
        {
            totalCodes += lengths[i];
        }

        // Validate symbols array has enough elements
        if (symbols.Length < totalCodes)
            throw new ArgumentException("Symbols array doesn't contain enough elements", nameof(symbols));

        var table = new CanonicalHuffmanTable();

        int code = 0;
        int symbolIndex = 0;

        for (int bits = 1; bits <= 16; bits++)
        {
            int count = lengths[bits - 1];

            for (int i = 0; i < count; i++)
            {
                if (symbolIndex >= symbols.Length)
                    throw new InvalidOperationException("Symbol index out of range");

                byte sym = symbols[symbolIndex++];
                table.Add(code, bits, sym);
                code++;
            }

            // Only shift if we're not at the last iteration
            if (bits < 16)
                code <<= 1;
        }

        return table;
    }

    public static List<MCUBlock> DecodeScanToBlocks(
        byte[] compressed,
        FrameInfo frame,
        ScanInfo scan,
        Dictionary<byte, QuantizationTable> qTables,
        Dictionary<byte, CanonicalHuffmanTable> huffDc,
        Dictionary<byte, CanonicalHuffmanTable> huffAc,
        int restartInterval = 0)
    {
        //foreach (var compressd in compressed)
        //{
        //    Console.Write($"{compressd:X2} ");
        //}

        int maxH = frame.Components.Max(c => c.HorizontalSampling);
        var maxV = frame.Components.Max(c => c.VerticalSampling);
        var compById = frame.Components.ToDictionary(c => c.Id);

        int mcuCountX = (frame.Width + (8 * maxH) - 1) / (8 * maxH);
        int mcuCountY = (frame.Height + (8 * maxV) - 1) / (8 * maxV);

        Console.WriteLine($"Image dimensions: {frame.Width}x{frame.Height}");
        Console.WriteLine($"Max sampling factors: H={maxH}, V={maxV}");
        Console.WriteLine($"Calculated MCU count: {mcuCountX}x{mcuCountY} = {mcuCountX * mcuCountY}");

        foreach (var comp in frame.Components)
        {
            Console.WriteLine($"Component {comp.Id}: H={comp.HorizontalSampling}, V={comp.VerticalSampling}");
            int compBlocksX = (frame.Width * comp.HorizontalSampling + (8 * maxH) - 1) / (8 * maxH);
            int compBlocksY = (frame.Height * comp.VerticalSampling + (8 * maxV) - 1) / (8 * maxV);
            Console.WriteLine($"  Blocks needed: {compBlocksX}x{compBlocksY} = {compBlocksX * compBlocksY}");
        }

        var reader = new BitReader(compressed, 0);

        // Previous DC predictors (quantized domain)
        var prevDc = new Dictionary<byte, int>();
        foreach (var c in frame.Components) prevDc[c.Id] = 0;

        var mcuList = new List<MCUBlock>(mcuCountX * mcuCountY);

        // Restart marker tracking
        int mcuCounter = 0;
        int nextRestartIndex = 0;

        for (int my = 0; my < mcuCountY; my++)
        {
            for (int mx = 0; mx < mcuCountX; mx++)
            {
                // Check for restart marker if needed
                if (restartInterval > 0 && mcuCounter % restartInterval == 0 && mcuCounter > 0)
                {
                    // We should be at a restart marker
                    reader.AlignToByte();

                    int m1 = reader.ReadRawByteOrMinusOne();
                    int m2 = reader.ReadRawByteOrMinusOne();
                    if (m1 < 0 || m2 < 0)
                        throw new InvalidOperationException("Unexpected EOF at restart check");
                    ushort marker = (ushort)((m1 << 8) | m2);
                    if (marker < 0xFFD0 || marker > 0xFFD7)
                        throw new InvalidOperationException($"Expected restart marker, found 0x{marker:X4}");

                    // Reset DC predictors as per JPEG standard
                    foreach (var key in prevDc.Keys.ToList())
                        prevDc[key] = 0;

                    nextRestartIndex = (nextRestartIndex + 1) % 8;
                }

                var mcu = new MCUBlock
                {
                    X = mx,
                    Y = my,
                    ComponentBlocks = new Dictionary<byte, List<short[]>>()
                };

                // Iterate components in the scan's order (SOS)
                foreach (var scanComp in scan.Components)
                {
                    byte compId = scanComp.ComponentId;

                    // Look up frame component to get sampling and quant table id
                    var frameComp = compById[compId];
                    int H = frameComp.HorizontalSampling;
                    int V = frameComp.VerticalSampling;

                    // Calculate actual blocks needed for partial MCUs at image boundaries
                    int blocksH = H;
                    int blocksV = V;

                    // Check if this is the last MCU in the row
                    if (mx == mcuCountX - 1)
                    {
                        int pixelsLeft = frame.Width - mx * 8 * maxH;
                        blocksH = (pixelsLeft + 8 * H - 1) / (8 * H);
                    }

                    // Check if this is the last MCU in the column
                    if (my == mcuCountY - 1)
                    {
                        int pixelsLeft = frame.Height - my * 8 * maxV;
                        blocksV = (pixelsLeft + 8 * V - 1) / (8 * V);
                    }

                    int blocksThisComp = blocksH * blocksV;

                    byte dcTblId = scanComp.DcHuffmanTableId;
                    byte acTblId = scanComp.AcHuffmanTableId;

                    if (!qTables.TryGetValue(frameComp.QuantizationTableId, out var qtbl))
                        throw new InvalidOperationException($"Missing quant table {frameComp.QuantizationTableId} for component {compId}.");
                    if (!huffDc.TryGetValue(dcTblId, out var dcTable))
                        throw new InvalidOperationException($"Missing DC Huffman table {dcTblId} for component {compId}.");
                    if (!huffAc.TryGetValue(acTblId, out var acTable))
                        throw new InvalidOperationException($"Missing AC Huffman table {acTblId} for component {compId}.");

                    var blocks = new List<short[]>(blocksThisComp);

                    for (int by = 0; by < blocksV; by++)
                    {
                        for (int bx = 0; bx < blocksH; bx++)
                        {
                            // -- Decode DC category and additional bits (signed)
                            int dcCategory = HuffmanDecodeSymbol(reader, dcTable);
                            if (dcCategory < 0)
                                throw new InvalidOperationException("Unexpected EOF while decoding DC category.");

                            int dcDiff = 0;
                            if (dcCategory > 0)
                            {
                                int bits = reader.ReadBits(dcCategory);
                                if (bits < 0) throw new InvalidOperationException("Unexpected EOF while reading DC additional bits.");
                                dcDiff = DecodeSigned(bits, dcCategory);
                            }

                            int dc = prevDc[compId] + dcDiff;
                            prevDc[compId] = dc;

                            // Prepare coefficient array in zigzag (scan) order
                            short[] coeffs = new short[64];
                            coeffs[0] = (short)dc; // Store raw quantized DC

                            // -- Decode AC coefficients
                            int k = 1;
                            while (k < 64)
                            {
                                int acSymbol = HuffmanDecodeSymbol(reader, acTable);
                                if (acSymbol < 0)
                                    throw new InvalidOperationException("Unexpected EOF while decoding AC symbol.");

                                if (acSymbol == 0x00)
                                {
                                    // EOB
                                    break;
                                }
                                if (acSymbol == 0xF0)
                                {
                                    // ZRL: skip 16 zeros
                                    k += 16;
                                    continue;
                                }

                                int run = acSymbol >> 4;
                                int size = acSymbol & 0x0F;
                                k += run;
                                if (k >= 64) break; // Safety check

                                var bits = reader.ReadBits(size);
                                if (bits < 0) throw new InvalidOperationException("Unexpected EOF while reading AC additional bits.");
                                int value = DecodeSigned(bits, size);
                                coeffs[k] = (short)value;
                                k++;
                            }

                            blocks.Add(coeffs);
                        }
                    }

                    mcu.ComponentBlocks[compId] = blocks;
                } // End scan components

                mcuList.Add(mcu);
                mcuCounter++;
            }
        }

        return mcuList;
    }

    // Helper that decodes one Huffman symbol by reading bits incrementally and checking the table
    public static int HuffmanDecodeSymbol(BitReader reader, CanonicalHuffmanTable table)
    {
        int code = 0;
        for (int length = 1; length <= 16; length++)
        {
            int bit = reader.ReadBit();
            if (bit < 0) return -1;
            code = (code << 1) | bit;
            if (table.TryGetSymbol(code, length, out byte sym))
                return sym;
        }
        // Invalid code
        throw new InvalidOperationException("Invalid Huffman code (no match up to length 16).");
    }

    // Sign-extend helper for JPEG additional bits to signed value
    public static int DecodeSigned(int bits, int size)
    {
        if (size == 0) return 0;
        int threshold = 1 << (size - 1);

        if (bits < threshold)
            return bits - (1 << size) + 1; // Negative value
        else
            return bits; // Positive value
    }

    public static bool ContainsRestartMarkers(byte[] data)
    {
        for (int i = 0; i < data.Length - 1; i++)
        {
            if (data[i] == 0xFF)
            {
                byte nextByte = data[i + 1];

                // Check if this is a restart marker (0xFFD0 to 0xFFD7)
                if (nextByte >= 0xD0 && nextByte <= 0xD7)
                {
                    return true;
                }

                // Skip byte stuffing (0xFF 0x00 is a literal 0xFF)
                if (nextByte == 0x00)
                {
                    i++; // Skip the next byte since it's part of byte stuffing
                }
            }
        }
        return false;
    }
}

public class MCUBlock
{
    public int X { get; set; }
    public int Y { get; set; }
    public Dictionary<byte, List<short[]>> ComponentBlocks { get; set; } = [];
}
