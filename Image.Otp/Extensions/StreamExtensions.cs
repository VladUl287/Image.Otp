namespace Image.Otp.Extensions;

public static class StreamExtensions
{
    public static int ReadBigEndianUInt16(this Stream stream)
    {
        int first = stream.ReadByte();
        if (first == -1)
            throw new InvalidDataException("Unexpected end of stream while reading 16-bit value.");

        int second = stream.ReadByte();
        if (second == -1)
            throw new InvalidDataException("Unexpected end of stream while reading 16-bit value.");

        return (first << 8) | second;
    }
}
