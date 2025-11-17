using Image.Otp.Core.Constants;

namespace Image.Otp.Core.Utils;

public sealed class HuffmanTable
{
    public byte[] Values { get; } = new byte[256];

    public ulong[] MaxCode { get; } = new ulong[18];

    public int[] ValOffset { get; } = new int[19];

    public byte[] LookaheadSize { get; } = new byte[Huffman.LookupSize];

    public byte[] LookaheadValue { get; } = new byte[Huffman.LookupSize];

    public HuffmanTable(ReadOnlySpan<byte> codeLengths, ReadOnlySpan<byte> values, Span<uint> workspace)
    {
        if (values.Length > Values.Length)
            throw new ArgumentException("Too many Huffman values");

        values.CopyTo(Values);

        GenerateHuffmanCodes(codeLengths, workspace);
        BuildDecodingTables(codeLengths, workspace);
        BuildLookupTables(codeLengths, workspace);
    }

    private static void GenerateHuffmanCodes(ReadOnlySpan<byte> codeLengths, Span<uint> workspace)
    {
        var currentCode = 0u;
        var symbolIndex = 0;

        for (var codeLength = 1; codeLength <= Huffman.MaxCodeLength; codeLength++)
        {
            var codeCount = codeLengths[codeLength - 1];

            for (var i = 0; i < codeCount; i++)
            {
                workspace[symbolIndex] = currentCode;
                symbolIndex++;
                currentCode++;
            }

            if (currentCode >= (1u << codeLength))
                throw new InvalidOperationException("Invalid Huffman table: codes overflow code length");

            currentCode <<= 1;
        }
    }

    private void BuildDecodingTables(ReadOnlySpan<byte> codeLengths, Span<uint> workspace)
    {
        var symbolIndex = 0;

        for (var codeLength = 1; codeLength <= Huffman.MaxCodeLength; codeLength++)
        {
            var codeCount = codeLengths[codeLength - 1];

            if (codeCount > 0)
            {
                ValOffset[codeLength] = symbolIndex - (int)workspace[symbolIndex];

                var maxCode = workspace[symbolIndex + codeCount - 1];
                MaxCode[codeLength] = LeftJustifyCode(maxCode, codeLength);

                symbolIndex += codeCount;
            }
            else
            {
                MaxCode[codeLength] = 0;
            }
        }

        ValOffset[18] = 0;
        MaxCode[17] = ulong.MaxValue;
    }

    private static ulong LeftJustifyCode(uint code, int codeLength)
    {
        var shiftAmount = Huffman.RegisterSize - codeLength;
        var leftJustified = (ulong)code << shiftAmount;
        return leftJustified | ((1ul << shiftAmount) - 1);
    }

    private void BuildLookupTables(ReadOnlySpan<byte> codeLengths, Span<uint> workspace)
    {
        Array.Fill(LookaheadSize, (byte)Huffman.SlowBits);

        var symbolIndex = 0;

        for (var codeLength = 1; codeLength <= Huffman.LookupBits; codeLength++)
        {
            var codeCount = codeLengths[codeLength - 1];
            var shiftAmount = Huffman.LookupBits - codeLength;

            for (var i = 0; i < codeCount; i++, symbolIndex++)
            {
                var lookupIndex = (int)(workspace[symbolIndex] << shiftAmount);
                var tableEntries = 1 << shiftAmount;

                for (var j = 0; j < tableEntries; j++)
                {
                    LookaheadSize[lookupIndex + j] = (byte)codeLength;
                    LookaheadValue[lookupIndex + j] = Values[symbolIndex];
                }
            }
        }
    }
}