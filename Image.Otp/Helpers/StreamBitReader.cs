namespace Image.Otp.Helpers;

public class StreamBitReader(Stream stream)
{
    public int BitBuffer { get; private set; } = 0;
    public int BitCount { get; private set; } = 0;
    public long Position => stream.Position;
    public bool CanSeek => stream.CanSeek;

    private int ReadByte()
    {
        if (!stream.CanRead) return -1;
        int b = stream.ReadByte();
        return b;
    }

    public int ReadBit()
    {
        if (BitCount == 0)
        {
            int b = ReadByte();
            if (b < 0) return -1;

            // Handle JPEG byte stuffing (0xFF followed by 0x00)
            if (b == 0xFF)
            {
                int next = ReadByte();
                if (next == -1) return -1;
                if (next != 0x00)
                {
                    // Put both bytes back into the stream
                    stream.Seek(-2, SeekOrigin.Current);
                    return -1;
                }
            }
            BitBuffer = b;
            BitCount = 8;
        }

        BitCount--;
        int bit = (BitBuffer >> BitCount) & 1;
        return bit;
    }

    public void AlignToByte()
    {
        BitBuffer = 0;
        BitCount = 0;
    }

    public int ReadBits(int n, bool signed = true)
    {
        if (n == 0) return 0;

        // Read the bits
        int[] bits = new int[n];
        for (int i = 0; i < n; i++)
        {
            int b = ReadBit();
            if (b < 0) return -1;
            bits[i] = b;
        }

        if (signed)
        {
            // Handle signed numbers (JPEG coefficient format)
            if (bits[0] == 1) // Positive number
            {
                return BitsToNumber(bits);
            }
            else // Negative number - flip all bits and make negative
            {
                int[] flippedBits = bits.Select(b => 1 - b).ToArray();
                return -BitsToNumber(flippedBits);
            }
        }
        else
        {
            // Unsigned number
            return BitsToNumber(bits);
        }
    }

    public int ReadRawByte()
    {
        AlignToByte();
        return ReadByte();
    }

    private static int BitsToNumber(int[] bits)
    {
        int res = 0;
        foreach (int bit in bits)
        {
            res = (res << 1) | bit;
        }
        return res;
    }

    public void Seek(long offset, SeekOrigin origin)
    {
        if (stream.CanSeek)
        {
            AlignToByte(); // Clear any buffered bits before seeking
            stream.Seek(offset, origin);
        }
        else
        {
            throw new NotSupportedException("Stream does not support seeking");
        }
    }
}