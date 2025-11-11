using System;
using System.Buffers;

namespace Image.Otp.Core.Utils;

public class ArrayBitReader(byte[] data)
{
    public int BitBuffer { get; private set; } = 0;
    public int BitCount { get; private set; } = 0;

    private readonly byte[] _data = data ?? throw new ArgumentNullException(nameof(data));

    private int _position = 0;

    private int ReadByte()
    {
        if (_position >= _data.Length) return -1;
        return _data[_position++];
    }

    public int ReadBit()
    {
        if (BitCount == 0)
        {
            int b = ReadByte();
            if (b < 0) return -1;

            if (b == 0xFF)
            {
                if (_position >= _data.Length) return -1;

                int next = _data[_position]; // Peek next byte without advancing
                if (next != 0x00)
                {
                    // Don't advance position for the second byte since we only peeked
                    return -1;
                }
                // Consume the 0x00 byte
                _position++;
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

    private static int BitsToNumber(Span<int> bits)
    {
        var res = 0;
        for (int i = 0; i < bits.Length; i++)
        {
            res = (res << 1) | bits[i];
        }
        return res;
    }

    // Add these fields to your class
    private int _savedBitBuffer = 0;
    private int _savedBitCount = 0;
    private int _savedPosition = 0;

    public int PeekBits(int n, bool signed = true)
    {
        SaveState();
        int result = ReadBits(n, signed);
        RestoreState();
        return result;
    }

    public void ConsumeBits(int n)
    {
        if (n < 0) throw new ArgumentOutOfRangeException(nameof(n), "Number of bits to consume cannot be negative");
        if (n == 0) return;

        // If we're consuming bits from the current buffer
        if (n <= BitCount)
        {
            BitCount -= n;
            // Shift out the consumed bits
            BitBuffer &= (1 << BitCount) - 1;
            return;
        }

        // If we need to consume more bits than available in current buffer
        int bitsRemaining = n - BitCount;
        BitBuffer = 0;
        BitCount = 0;

        // Calculate how many full bytes we need to consume
        int bytesToConsume = bitsRemaining / 8;
        int extraBits = bitsRemaining % 8;

        // Consume full bytes
        for (int i = 0; i < bytesToConsume; i++)
        {
            if (ReadByte() == -1) return;
        }

        // Consume remaining bits
        if (extraBits > 0)
        {
            ReadBits(extraBits, false);
        }
    }

    // Helper methods for state management
    private void SaveState()
    {
        _savedBitBuffer = BitBuffer;
        _savedBitCount = BitCount;
        _savedPosition = _position;
    }

    private void RestoreState()
    {
        BitBuffer = _savedBitBuffer;
        BitCount = _savedBitCount;
        _position = _savedPosition;
    }
}
