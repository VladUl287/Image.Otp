namespace Image.Otp.Core.Extensions;

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

    public static int ReadBigEndianUInt16(this Span<byte> stream, int start)
    {
        int first = stream[start];
        if (first == -1)
            throw new InvalidDataException("Unexpected end of stream while reading 16-bit value.");

        int second = stream[start + 1];
        if (second == -1)
            throw new InvalidDataException("Unexpected end of stream while reading 16-bit value.");

        return (first << 8) | second;
    }
}
