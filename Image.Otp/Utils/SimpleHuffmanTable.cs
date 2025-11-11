namespace Image.Otp.Core.Utils;

public interface IHuffmanTable
{
    (byte symbol, byte length) Decode(uint next16Bits);
}

public sealed class SimpleHuffmanTable : IHuffmanTable
{
    // Fast lookup for short codes (8 bits or less)
    private readonly byte[] _lookupSize = new byte[256];    // Code length for each 8-bit pattern
    private readonly byte[] _lookupValue = new byte[256];   // Symbol for each 8-bit pattern

    // For longer codes (9-16 bits) - we use the original efficient method
    private readonly byte[] _symbols;                       // All symbols in order
    private readonly ushort[] _maxCode = new ushort[17];    // Largest code for each length (1-16)
    private readonly short[] _valOffset = new short[17];    // Index offset for each length

    public SimpleHuffmanTable(ReadOnlySpan<byte> codeLengths, ReadOnlySpan<byte> values)
    {
        _symbols = values.ToArray();
        BuildTables(codeLengths);
    }

    private void BuildTables(ReadOnlySpan<byte> codeLengths)
    {
        // Step 1: Generate canonical Huffman codes
        var codes = new ushort[256]; // Store the code for each symbol
        ushort nextCode = 0;
        int symbolIndex = 0;

        // For each possible code length (1 to 16 bits)
        for (int length = 1; length <= 16; length++)
        {
            int count = codeLengths[length]; // How many symbols have this code length?

            // Assign consecutive codes to symbols of this length
            for (int i = 0; i < count; i++)
            {
                codes[symbolIndex] = nextCode;
                nextCode++;
                symbolIndex++;
            }

            nextCode = (ushort)(nextCode << 1); // Move to next code length
        }

        // Step 2: Build fast lookup table for codes 8 bits or shorter
        BuildFastLookup(codeLengths, codes);

        // Step 3: Build tables for longer codes (9-16 bits)
        BuildLongCodeTables(codeLengths, codes);
    }

    private void BuildFastLookup(ReadOnlySpan<byte> codeLengths, ushort[] codes)
    {
        // Initialize all entries as "invalid"
        for (int i = 0; i < 256; i++)
        {
            _lookupSize[i] = 0; // 0 means "not in fast lookup"
        }

        int symbolIndex = 0;

        // Process codes that are 8 bits or shorter
        for (int length = 1; length <= 8; length++)
        {
            int count = codeLengths[length];

            for (int i = 0; i < count; i++)
            {
                ushort code = codes[symbolIndex];
                byte symbol = _symbols[symbolIndex];

                // A code of length N needs to be left-aligned in 8 bits
                // Example: code '110' (3 bits) becomes '11000000' in 8 bits
                int leftAlignedCode = code << (8 - length);

                // How many different bit patterns can follow this code?
                // For 3-bit code in 8-bit space: 2^(8-3) = 32 patterns
                int patterns = 1 << (8 - length);

                // Fill all possible patterns that start with this code
                for (int pattern = 0; pattern < patterns; pattern++)
                {
                    int lookupIndex = leftAlignedCode | pattern;
                    _lookupSize[lookupIndex] = (byte)length;
                    _lookupValue[lookupIndex] = symbol;
                }

                symbolIndex++;
            }
        }
    }

    private void BuildLongCodeTables(ReadOnlySpan<byte> codeLengths, ushort[] codes)
    {
        int symbolIndex = 0;

        for (int length = 1; length <= 16; length++)
        {
            int count = codeLengths[length];

            if (count > 0)
            {
                // First symbol index for this length
                int firstIndex = symbolIndex;
                // First code for this length  
                ushort firstCode = codes[firstIndex];
                // Last code for this length
                ushort lastCode = codes[firstIndex + count - 1];

                // MaxCode: the largest code of this length, left-aligned in 16 bits
                _maxCode[length] = (ushort)(lastCode << (16 - length));

                // ValOffset: to convert from code to symbol index
                // index = code + offset
                _valOffset[length] = (short)(firstIndex - firstCode);

                symbolIndex += count;
            }
            else
            {
                _maxCode[length] = 0; // No codes of this length
            }
        }
    }

    public (byte symbol, byte length) Decode(uint next16Bits)
    {
        // Step 1: Try fast 8-bit lookup first
        byte fastLength = _lookupSize[next16Bits >> 8]; // Get top 8 bits

        if (fastLength > 0)
        {
            byte symbol = _lookupValue[next16Bits >> 8];
            return (symbol, fastLength);
        }

        // Step 2: Handle longer codes (9-16 bits)
        return DecodeLongCode(next16Bits);
    }

    private (byte symbol, byte length) DecodeLongCode(uint next16Bits)
    {
        ushort bits16 = (ushort)(next16Bits >> (32 - 16)); // Get the next 16 bits

        // Check each possible code length from 9 to 16
        for (int length = 9; length <= 16; length++)
        {
            // Left-align the code in 16 bits for comparison
            ushort leftAligned = (ushort)(bits16 >> (16 - length));

            if (leftAligned <= _maxCode[length])
            {
                // Found a valid code! Calculate which symbol it represents
                int index = _valOffset[length] + leftAligned;
                byte symbol = _symbols[index];
                return (symbol, (byte)length);
            }
        }

        throw new InvalidOperationException("Invalid Huffman code");
    }
}
