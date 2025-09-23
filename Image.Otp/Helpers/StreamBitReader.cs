using System.Buffers;

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

            if (b == 0xFF)
            {
                int next = ReadByte();
                if (next == -1) return -1;
                if (next != 0x00)
                {
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

        const int StackAllocThreshold = 64;

        if (n <= StackAllocThreshold)
        {
            Span<int> bits = stackalloc int[n];

            for (int i = 0; i < n; i++)
            {
                int b = ReadBit();
                if (b < 0) return -1;
                bits[i] = b;
            }

            return ProcessBits(bits, signed);
        }
        else
        {
            var bits = ArrayPool<int>.Shared.Rent(n);
            try
            {
                for (int i = 0; i < n; i++)
                {
                    int b = ReadBit();
                    if (b < 0) return -1;
                    bits[i] = b;
                }

                return ProcessBits(bits.AsSpan(0, n), signed);
            }
            finally
            {
                ArrayPool<int>.Shared.Return(bits);
            }
        }
    }

    private static int ProcessBits(Span<int> bits, bool signed)
    {
        if (signed)
        {
            if (bits[0] == 1) // Positive
            {
                return BitsToNumber(bits);
            }
            else // Negative
            {
                for (int i = 0; i < bits.Length; i++)
                {
                    bits[i] = 1 - bits[i];
                }
                return -BitsToNumber(bits);
            }
        }
        else
        {
            return BitsToNumber(bits);
        }
    }

    public int ReadRawByte()
    {
        AlignToByte();
        return ReadByte();
    }

    private static int BitsToNumber(Span<int> bits) => BitsToNumber(bits, bits.Length);
    private static int BitsToNumber(Span<int> bits, int length)
    {
        var res = 0;
        for (int i = 0; i < length; i++)
        {
            res = (res << 1) | bits[i];
        }
        return res;
    }
}