namespace Image.Otp.Models.Jpeg;

public sealed class CanonicalHuffmanTable
{
    private ushort[] _codeData; // Packed: symbol in low 8 bits, code length in high 8 bits
    private readonly ushort[] _firstCode; // First code for each length (1-16)
    private readonly byte[] _symbolIndex; // Index into _codeData for each length

    public CanonicalHuffmanTable()
    {
        _codeData = [];
        _firstCode = new ushort[17]; // Index 1-16
        _symbolIndex = new byte[17]; // Index 1-16
    }

    public void Initialize(Span<byte> lengths, Span<byte> symbols)
    {
        if (lengths.Length != 16)
            throw new ArgumentException("Lengths array must have exactly 16 elements", nameof(lengths));

        // Calculate total symbols
        int totalSymbols = 0;
        for (int i = 0; i < 16; i++)
            totalSymbols += lengths[i];

        if (symbols.Length < totalSymbols)
            throw new ArgumentException("Symbols array doesn't contain enough elements", nameof(symbols));

        // Allocate arrays (reuse if possible, but we'll create new ones for simplicity)
        _codeData = new ushort[totalSymbols];

        // Build canonical codes
        int code = 0;
        int symbolIndex = 0;
        int dataIndex = 0;

        for (int bits = 1; bits <= 16; bits++)
        {
            int count = lengths[bits - 1];
            _firstCode[bits] = (ushort)code;
            _symbolIndex[bits] = (byte)dataIndex;

            for (int i = 0; i < count; i++)
            {
                byte symbol = symbols[symbolIndex++];
                // Pack symbol (low 8 bits) and code length (high 8 bits)
                _codeData[dataIndex++] = (ushort)((bits << 8) | symbol);
                code++;
            }

            if (bits < 16)
                code <<= 1;
        }
    }

    public bool TryGetSymbol(int code, int length, out byte symbol)
    {
        symbol = 0;

        if (length <= 0 || length > 16)
            return false;

        int firstCode = _firstCode[length];
        if (code < firstCode)
            return false;

        int index = _symbolIndex[length] + (code - firstCode);

        if (index >= _codeData.Length)
            return false;

        // Verify this entry has the correct length (sanity check)
        ushort packed = _codeData[index];
        int entryLength = packed >> 8;

        if (entryLength != length)
            return false;

        symbol = (byte)(packed & 0xFF);
        return true;
    }
}

public sealed class HuffmanTableLogic
{
    public static CanonicalHuffmanTable BuildCanonical(byte[] lengths, byte[] symbols)
    {
        if (lengths == null || lengths.Length != 16)
            throw new ArgumentException("Lengths array must have exactly 16 elements", nameof(lengths));

        symbols ??= [];
        return BuildCanonical(lengths.AsSpan(), symbols.AsSpan());
    }

    public static CanonicalHuffmanTable BuildCanonical(Span<byte> lengths, Span<byte> symbols)
    {
        if (lengths.Length != 16)
            throw new ArgumentException("Lengths array must have exactly 16 elements", nameof(lengths));

        var table = new CanonicalHuffmanTable();
        table.Initialize(lengths, symbols);
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

public sealed class MCUBlock
{
    public int X { get; init; }
    public int Y { get; init; }
    public Dictionary<byte, List<short[]>> ComponentBlocks { get; init; } = [];
}