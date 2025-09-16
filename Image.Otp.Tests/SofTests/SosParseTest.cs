using Image.Otp.Models.Jpeg;
using Image.Otp.Parsers;

namespace Image.Otp.Tests.SofTests;

public sealed class SosParseTest
{
    [Fact]
    public void ParseSofSegment_Baseline3Components_Works()
    {
        // Build a fake SOF0 payload
        var payload = new List<byte>();
        payload.Add(8);            // precision
        payload.Add(0x00); payload.Add(0xF0); // height = 240
        payload.Add(0x01); payload.Add(0x00); // width = 256
        payload.Add(3);            // 3 components

        // Component 1 (Y)
        payload.Add(1);    // ID
        payload.Add(0x22); // sampling factor (H=2, V=2)
        payload.Add(0);    // Q-table ID

        // Component 2 (Cb)
        payload.Add(2);
        payload.Add(0x11); // H=1, V=1
        payload.Add(1);

        // Component 3 (Cr)
        payload.Add(3);
        payload.Add(0x11);
        payload.Add(1);

        var seg = new JpegSegment { Marker = 0xC0, Data = payload.ToArray() };
        var frame = JpegTableDecoder.ParseSofSegment(seg);

        Assert.Equal(256, frame.Width);
        Assert.Equal(240, frame.Height);
        Assert.Equal(3, frame.Components.Count);

        Assert.Equal(1, frame.Components[0].Id);
        Assert.Equal(0x22, frame.Components[0].SamplingFactor);
        Assert.Equal(0, frame.Components[0].QuantizationTableId);
    }

    [Fact]
    public void ParseSosSegment_ThreeComponents_Works()
    {
        var payload = new List<byte>();
        payload.Add(3); // number of components

        // Component 1
        payload.Add(1); // ID
        payload.Add(0x00); // DC=0, AC=0

        // Component 2
        payload.Add(2);
        payload.Add(0x11); // DC=1, AC=1

        // Component 3
        payload.Add(3);
        payload.Add(0x11); // DC=1, AC=1

        // Ss, Se, AhAl
        payload.Add(0x00); // Ss
        payload.Add(0x3F); // Se=63
        payload.Add(0x00); // Ah=0, Al=0

        var seg = new JpegSegment { Marker = 0xDA, Data = payload.ToArray() };
        var scan = JpegTableDecoder.ParseSosSegment(seg);

        Assert.Equal(3, scan.Components.Count);
        Assert.Equal(1, scan.Components[0].ComponentId);
        Assert.Equal(0, scan.Components[0].DcHuffmanTableId);
        Assert.Equal(0, scan.Components[0].AcHuffmanTableId);

        Assert.Equal(2, scan.Components[1].ComponentId);
        Assert.Equal(1, scan.Components[1].DcHuffmanTableId);
        Assert.Equal(1, scan.Components[1].AcHuffmanTableId);

        Assert.Equal(0, scan.Ss);
        Assert.Equal(63, scan.Se);
        Assert.Equal(0, scan.Ah);
        Assert.Equal(0, scan.Al);
    }
}
