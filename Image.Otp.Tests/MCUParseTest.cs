using Image.Otp.Models.Jpeg;

namespace Image.Otp.Tests;

// Make sure to using the namespace(s) where your production types live.
// e.g. using MyJpegLib;
public class DecodeScanToBlocksTests
{
    [Fact]
    public void DecodeScanToBlocks_SingleBlock_Dc5_IsDecoded()
    {
        // --- 1) Build Frame + Scan ---
        var frame = new FrameInfo
        {
            Width = 8,
            Height = 8,
            Components = new List<ComponentInfo>
            {
                new ComponentInfo { Id = 1, SamplingFactor = 0x11, QuantizationTableId = 0 }
            }
        };

        var scan = new ScanInfo
        {
            Components = new List<ScanComponent>
            {
                new ScanComponent { ComponentId = 1, DcHuffmanTableId = 0, AcHuffmanTableId = 0 }
            }
        };

        // --- 2) QTable (all ones so we don't change values) ---
        var qTables = new Dictionary<byte, QuantizationTable>
        {
            [0] = new QuantizationTable { Id = 0, Values = Enumerable.Repeat((ushort)1, 64).ToArray() }
        };

        // --- 3) Build tiny Huffman tables for test ---
        // DC table: one code of length 1 => symbol = 3 (category 3)
        byte[] dcLengths = new byte[16]; dcLengths[0] = 1; // one code with length=1
        byte[] dcSymbols = new byte[] { 3 }; // this Huffman symbol means "category=3" for DC
        var (dcTable, dcMapping) = BuildSimpleHuffmanTable(dcLengths, dcSymbols);

        // AC table: one code of length 1 => symbol = 0x00 (EOB)
        byte[] acLengths = new byte[16]; acLengths[0] = 1;
        byte[] acSymbols = new byte[] { 0x00 }; // EOB
        var (acTable, acMapping) = BuildSimpleHuffmanTable(acLengths, acSymbols);

        var huffDc = new Dictionary<byte, CanonicalHuffmanTable> { [0] = dcTable };
        var huffAc = new Dictionary<byte, CanonicalHuffmanTable> { [0] = acTable };

        // --- 4) Build compressed bitstream encoding DC = 5 ---
        // Determine DC category and bits (positive values)
        int dcValue = 5;
        int dcCategory = GetMagnitudeCategory(dcValue); // should be 3 for value 5
        Assert.Equal(3, dcCategory);

        // Get Huffman code/length for the DC symbol (category=3)
        // dcMapping maps symbol -> (code, length)
        var (dcCode, dcCodeLen) = dcMapping[(byte)dcSymbols[0]]; // the code that emits the DC category
        var (acCode, acCodeLen) = acMapping[(byte)acSymbols[0]]; // code for EOB

        // Bits for the DC difference: for positive values it's the binary representation (size=category)
        int dcBits = dcValue & ((1 << dcCategory) - 1);

        // Build bitstream: [dcCode (dcCodeLen bits)] [dcBits (dcCategory bits)] [acCode (acCodeLen bits)]
        byte[] compressed = BuildBitstreamPackedMsbFirst(
            new (int bitsValue, int bitsLength)[]
            {
                (dcCode, dcCodeLen),
                (dcBits, dcCategory),
                (acCode, acCodeLen)
            }
        );

        // --- 5) Call production method under test ---
        var mcus = HuffmanTableLogic.DecodeScanToBlocks(compressed, frame, scan, qTables, huffDc, huffAc);

        // --- 6) Assert expected decoding ---
        Assert.Single(mcus);
        var mcu = mcus[0];
        Assert.True(mcu.ComponentBlocks.ContainsKey(1));
        var blocks = mcu.ComponentBlocks[1];
        Assert.Single(blocks);
        var block = blocks[0];

        // DC should be 5 (we encoded DC diff = 5 and prevDc initial is 0)
        Assert.Equal(5, block[0]);
        // All AC should be zero
        for (int i = 1; i < 64; i++) Assert.Equal(0, block[i]);
    }

    // -------------------
    // Helper utilities
    // -------------------

    // Build canonical Huffman table and also return a mapping symbol -> (code, length)
    // Uses the same canonical algorithm you already use in production
    private static (CanonicalHuffmanTable table, Dictionary<byte, (int code, int length)> mapping)
        BuildSimpleHuffmanTable(byte[] lengths, byte[] symbols)
    {
        var table = new CanonicalHuffmanTable();
        var mapping = new Dictionary<byte, (int code, int length)>();

        int code = 0;
        int symbolIndex = 0;
        for (int bits = 1; bits <= 16; bits++)
        {
            int count = (bits <= lengths.Length) ? lengths[bits - 1] : 0;
            for (int i = 0; i < count; i++)
            {
                if (symbolIndex >= symbols.Length)
                    throw new ArgumentException("Not enough symbols for provided lengths.");

                byte sym = symbols[symbolIndex++];
                // add to canonical table (production expects Add(code, length, symbol))
                table.Add(code, bits, sym);
                mapping[sym] = (code, bits);
                code++;
            }
            code <<= 1;
        }

        return (table, mapping);
    }

    // Pack an array of (value, length) into MSB-first bytes (like JPEG bitstream)
    // Each (value, length) must be given as an integer where only the lower 'length' bits matter.
    private static byte[] BuildBitstreamPackedMsbFirst((int bitsValue, int bitsLength)[] pieces)
    {
        var outBytes = new List<byte>();
        int curByte = 0;
        int curBitsUsed = 0; // how many bits are already in current byte (from MSB side)

        foreach (var (bitsValue, bitsLength) in pieces)
        {
            int remaining = bitsLength;
            // write bits from most-significant bit of bitsValue (leftmost of the 'length' bits)
            while (remaining > 0)
            {
                int toWrite = Math.Min(8 - curBitsUsed, remaining);
                // position inside bitsValue: we want the top 'remaining' bits.
                int shift = remaining - toWrite;
                int mask = ((1 << toWrite) - 1);
                int chunk = (bitsValue >> shift) & mask;

                // place chunk into curByte at correct position (MSB-first)
                int shiftIntoByte = 8 - curBitsUsed - toWrite;
                curByte |= (chunk << shiftIntoByte);
                curBitsUsed += toWrite;
                remaining -= toWrite;

                if (curBitsUsed == 8)
                {
                    outBytes.Add((byte)curByte);
                    curByte = 0;
                    curBitsUsed = 0;
                }
            }
        }

        // pad final byte with zeros on the right if needed
        if (curBitsUsed > 0)
        {
            outBytes.Add((byte)curByte);
        }

        return outBytes.ToArray();
    }

    // Magnitude category for JPEG (category = number of bits needed to represent absolute value)
    private static int GetMagnitudeCategory(int value)
    {
        if (value == 0) return 0;
        int absv = Math.Abs(value);
        int cat = 0;
        while (absv > 0)
        {
            cat++;
            absv >>= 1;
        }
        return cat;
    }
}