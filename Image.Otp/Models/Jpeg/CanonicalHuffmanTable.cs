namespace Image.Otp.Models.Jpeg;

public sealed class CanonicalHuffmanTable
{
    private ushort[] _codeData; // Packed: symbol in low 8 bits, code length in high 8 bits
    private readonly ushort[] _firstCode; // First code for each length (1-16)
    private readonly byte[] _symbolIndex; // Index into _codeData for each length

    public CanonicalHuffmanTable(Span<byte> lengths, Span<byte> symbols)
    {
        _codeData = [];
        _firstCode = new ushort[17]; // Index 1-16
        _symbolIndex = new byte[17]; // Index 1-16

        Initialize(lengths, symbols);
    }

    private void Initialize(Span<byte> lengths, Span<byte> symbols)
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

        return new CanonicalHuffmanTable(lengths, symbols);
    }
}

public sealed class MCUBlock
{
    public int X { get; init; }
    public int Y { get; init; }
    public Dictionary<byte, List<short[]>> ComponentBlocks { get; init; } = [];
}