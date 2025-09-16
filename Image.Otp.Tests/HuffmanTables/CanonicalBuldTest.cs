using Image.Otp.Models.Jpeg;

namespace Image.Otp.Tests.HuffmanTables;

public sealed class CanonicalBuldTest
{
    [Fact]
    public void DecodeSigned_CorrectForVariousSizes()
    {
        Assert.Equal(-1, HuffmanTableLogic.DecodeSigned(0, 1)); // size=1 raw=0 -> -1
        Assert.Equal(1, HuffmanTableLogic.DecodeSigned(1, 1)); // size=1 raw=1 -> +1

        Assert.Equal(-2, HuffmanTableLogic.DecodeSigned(1, 2)); // size=2 raw=01 -> -2
        Assert.Equal(2, HuffmanTableLogic.DecodeSigned(2, 2)); // raw=10 -> +2

        Assert.Equal(-4, HuffmanTableLogic.DecodeSigned(3, 3)); // raw=011 -> -4
        Assert.Equal(5, HuffmanTableLogic.DecodeSigned(5, 3)); // raw=101 -> +5
    }

    [Fact]
    public void CanonicalHuffmanTable_BuildAndDecodeSymbol()
    {
        // lengths: one code of length 1 (count=1), rest zero
        byte[] lengths = new byte[16]; lengths[0] = 2; // two codes of length 1 for test
        byte[] symbols = new byte[] { 0x41, 0x42 };   // 'A' and 'B'

        var table = HuffmanTableLogic.BuildCanonical(lengths, symbols);

        // codebook: for len=1 we expect two codes: 0 and 1 (msb-first reading)
        // build a bitstream: '0' -> 0x41, '1' -> 0x42
        var bits = new byte[] { 0b01000000 }; // first bit 0 => 'A', then 1 => 'B' (we gave only first two bits)
        var br = new BitReader(bits);

        Assert.Equal(0x41, HuffmanTableLogic.HuffmanDecodeSymbol(br, table));
        Assert.Equal(0x42, HuffmanTableLogic.HuffmanDecodeSymbol(br, table));
    }

    [Fact]
    public void BuildCanonical_SimpleTable_Works()
    {
        byte[] lengths = new byte[16];
        lengths[0] = 2; // 2 codes of length 1
        lengths[1] = 1; // 1 code of length 2

        byte[] symbols = "ABC"u8.ToArray();

        var table = HuffmanTableLogic.BuildCanonical(lengths, symbols);

        // Check the first two symbols (length 1)
        Assert.True(table.TryGetSymbol(0, 1, out byte sym0));
        Assert.Equal((byte)'A', sym0);

        Assert.True(table.TryGetSymbol(1, 1, out byte sym1));
        Assert.Equal((byte)'B', sym1);

        // Check the third symbol (length 2)
        Assert.True(table.TryGetSymbol(4, 2, out byte sym2));
        Assert.Equal((byte)'C', sym2);
    }

    [Fact]
    public void BuildCanonical_MultipleLengths_Works()
    {
        byte[] lengths = new byte[16];
        lengths[0] = 1; // 1 code of length 1
        lengths[1] = 2; // 2 codes of length 2
        lengths[2] = 1; // 1 code of length 3

        byte[] symbols = [0x10, 0x11, 0x12, 0x13];

        var table = HuffmanTableLogic.BuildCanonical(lengths, symbols);

        // Length 1
        Assert.True(table.TryGetSymbol(0, 1, out var s0));
        Assert.Equal(0x10, s0);

        // Length 2 codes start at code=2 (after shifting)
        Assert.True(table.TryGetSymbol(2, 2, out var s1));
        Assert.Equal(0x11, s1);

        Assert.True(table.TryGetSymbol(3, 2, out var s2));
        Assert.Equal(0x12, s2);

        // Length 3 code
        Assert.True(table.TryGetSymbol(8, 3, out var s3));
        Assert.Equal(0x13, s3);
    }
}
