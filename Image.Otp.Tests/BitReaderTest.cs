using Image.Otp.Models.Jpeg;

namespace Image.Otp.Tests;

public sealed class BitReaderTest
{
    [Fact]
    public void BitReader_HandlesFF00Stuffing_And_MSBOrder()
    {
        // bytes: 0b10101010, 0xFF 0x00 (stuffed => single 0xFF), 0b11001100
        var data = new byte[] { 0b10101010, 0xFF, 0x00, 0b11001100 };
        var br = new BitReader(data);

        Assert.Equal(0b101, br.ReadBits(3)); // first 3 bits of 0b10101010
        Assert.Equal(0b01010, br.ReadBits(5)); // remaining 5 bits of first byte
        Assert.Equal(0xFF, br.ReadBits(8)); // stuffed byte should be read as 0xFF
        Assert.Equal(0b1100, br.ReadBits(4)); // top 4 bits of last byte
    }
}
