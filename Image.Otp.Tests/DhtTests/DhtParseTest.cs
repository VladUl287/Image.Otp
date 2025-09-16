using Image.Otp.Models.Jpeg;
using Image.Otp.Parsers;

namespace Image.Otp.Tests.DhtTests;

public sealed class DhtParseTest
{
    [Fact]
    public void ParseDhtSegments_SingleTable_Works()
    {
        // Build payload
        var dhtPayload = new List<byte>
        {
            0x00, // class=0 (DC), id=0,
            2,1,0,0,0,0,0,0,0,0,0,0,0,0,0,0,  // lengths,
            0x05, 0x06, 0x07
        };

        var segment = new JpegSegment
        {
            Marker = 0xC4,
            Data = [.. dhtPayload]
        };

        var result = JpegTableDecoder.ParseDhtSegments([segment]);

        Assert.Single(result);
        var table = result[0];
        Assert.Equal(0, table.Class); // DC
        Assert.Equal(0, table.Id);
        Assert.Equal(16, table.CodeLengths.Length);
        Assert.Equal(2, table.CodeLengths[0]); // 2 codes length 1
        Assert.Equal(1, table.CodeLengths[1]); // 1 code length 2
        Assert.Equal(3, table.Symbols.Length);
        Assert.Equal(new byte[] { 0x05, 0x06, 0x07 }, table.Symbols);
    }

    [Fact]
    public void ParseDhtSegments_MultipleTables_Works()
    {
        var payload = new List<byte>
        {
            // --- First table: DC0 ---
            0x00 // class=0, id=0
        };
        payload.AddRange(
        [
            2,1,0,0,0,0,0,0,0,0,0,0,0,0,0,0
        ]);
        payload.AddRange([0x01, 0x02, 0x03]);

        // --- Second table: AC0 ---
        payload.Add(0x10); // class=1, id=0
        payload.AddRange(
        [
            0,0,1,1,0,0,0,0,0,0,0,0,0,0,0,0
        ]);
        payload.AddRange([0xAA, 0xBB]);

        var segment = new JpegSegment
        {
            Marker = 0xC4,
            Data = [.. payload]
        };

        var result = JpegTableDecoder.ParseDhtSegments(new List<JpegSegment> { segment });

        Assert.Equal(2, result.Count);

        // Check first table
        Assert.Equal(0, result[0].Class); // DC
        Assert.Equal(0, result[0].Id);
        Assert.Equal(3, result[0].Symbols.Length);

        // Check second table
        Assert.Equal(1, result[1].Class); // AC
        Assert.Equal(0, result[1].Id);
        Assert.Equal(2, result[1].Symbols.Length);
    }
}
