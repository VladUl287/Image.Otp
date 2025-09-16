using Image.Otp.Models.Jpeg;
using Image.Otp.Parsers;

namespace Image.Otp.Tests.DqtTests;

public sealed class DqtParseTest
{
    // DQT payload = [pqTq, then 64 values]
    private static readonly byte[] dqtPayload =
    [
        0x00, // PQ=0 (8-bit), Tq=0
        // 64 values in zig-zag order
        16,11,10,16,24,40,51,61,
        12,12,14,19,26,58,60,55,
        14,13,16,24,40,57,69,56,
        14,17,22,29,51,87,80,62,
        18,22,37,56,68,109,103,77,
        24,35,55,64,81,104,113,92,
        49,64,78,87,103,121,120,101,
        72,92,95,98,112,100,103,99
    ];

    private static readonly JpegSegment DQTSegment = new()
    {
        Marker = 0xDB,
        Data = dqtPayload
    };

    [Fact]
    public void Bit_8_Table_PQ_0()
    {
        var result = JpegTableDecoder.ParseDqtSegments([DQTSegment]);

        Assert.True(result.ContainsKey(0)); // table ID=0 must exist
        var table = result[0];
        Assert.Equal(64, table.Values.Length); // must have 64 entries
        Assert.Equal((ushort)16, table.Values[0]); // first value = 16
        Assert.Equal((ushort)99, table.Values[63]); // last value = 99
    }

    [Fact]
    public void Bit_16_Table_PQ_1()
    {
        var dqtPayload16 = BuildDqtPayload16();

        var segment = new JpegSegment { Marker = 0xDB, Data = dqtPayload16 };
        var result = JpegTableDecoder.ParseDqtSegments([segment]);

        Assert.True(result.ContainsKey(1));
        Assert.Equal(64, result[1].Values.Length);
        Assert.Equal((ushort)16, result[1].Values[0]); // 0x0010
        Assert.Equal((ushort)17, result[1].Values[1]); // 0x0011
    }

    [Fact]
    public void ParseDqtSegments_MultipleTables_Works()
    {
        var dqtPayload = BuildDqtPayload_MultipleTables();

        var segment = new JpegSegment
        {
            Marker = 0xDB, // DQT
            Data = dqtPayload
        };

        var result = JpegTableDecoder.ParseDqtSegments([segment]);

        // Table 0 (8-bit)
        Assert.Equal(64, result[0].Values.Length);
        Assert.Equal(1, result[0].Values[0]);   // first
        Assert.Equal(64, result[0].Values[63]); // last

        // Table 1 (16-bit)
        Assert.Equal(64, result[1].Values.Length);
        Assert.Equal(100, result[1].Values[0]);  // first
        Assert.Equal(163, result[1].Values[63]); // last
    }

    private static byte[] BuildDqtPayload16()
    {
        var list = new List<byte>();

        // First byte: pqTq = 0x11 → PQ=1 (16-bit), Tq=1
        list.Add(0x11);

        // Generate 64 sequential values starting at 16
        for (int i = 0; i < 64; i++)
        {
            ushort val = (ushort)(16 + i);
            list.Add((byte)(val >> 8));   // high byte first
            list.Add((byte)(val & 0xFF)); // low byte second
        }

        return list.ToArray();
    }

    private static byte[] BuildDqtPayload_MultipleTables()
    {
        var list = new List<byte>();

        // ---- Table 0: 8-bit values, Tq=0 ----
        // pqTq = 0x00 → PQ=0 (8-bit), Tq=0
        list.Add(0x00);
        for (int i = 0; i < 64; i++)
        {
            list.Add((byte)(1 + i)); // values: 1..64
        }

        // ---- Table 1: 16-bit values, Tq=1 ----
        // pqTq = 0x11 → PQ=1 (16-bit), Tq=1
        list.Add(0x11);
        for (int i = 0; i < 64; i++)
        {
            ushort val = (ushort)(100 + i); // values: 100..163
            list.Add((byte)(val >> 8));   // high byte
            list.Add((byte)(val & 0xFF)); // low byte
        }

        return list.ToArray();
    }
}
