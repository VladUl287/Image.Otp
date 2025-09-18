namespace Image.Otp.Helpers;

public sealed class CustomBitReader(byte[] data)
{
    public readonly byte[] data = data ?? throw new ArgumentNullException(nameof(data));
    public int pos = 0;
    public int bitBuffer = 0;
    public int bitCount = 0;

    private int ReadByte()
    {
        if (pos >= data.Length) return -1;
        return data[pos++];
    }

    public int ReadBit()
    {
        if (bitCount == 0)
        {
            int b = ReadByte();
            if (b < 0) return -1;
            if (b == 0xFF)
            {
                int next = ReadByte();
                if (next == -1) return -1;
                if (next != 0x00)
                {
                    pos -= 2;
                    return -1;
                }
            }
            bitBuffer = b;
            bitCount = 8;
        }

        bitCount--;
        int bit = (bitBuffer >> bitCount) & 1;
        return bit;
    }

    public void AlignToByte()
    {
        bitBuffer = 0;
        bitCount = 0;
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
}

