using System.Buffers;

namespace Image.Otp.Core.Utils;

public sealed class JpegBitReader(Stream stream) : IBitReader
{
    public int BitBuffer { get; private set; } = 0;
    public int BitCount { get; private set; } = 0;

    public int ReadBit()
    {
        if (BitCount == 0)
        {
            var b = ReadByte();
            if (b < 0) return -1;

            if (b == 0xFF)
            {
                var next = ReadByte();
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
        return (BitBuffer >> BitCount) & 1;
    }

    public int ReadBits(int n, bool signed = true)
    {
        var bits = ArrayPool<int>.Shared.Rent(n);
        try
        {
            for (var i = 0; i < n; i++)
            {
                var b = ReadBit();
                if (b < 0) return -1;
                bits[i] = b;
            }
            return bits.AsSpan(0, n).ToNumber(signed);
        }
        finally
        {
            ArrayPool<int>.Shared.Return(bits);
        }
    }

    public int PeekBits(int n, bool signed = true)
    {
        SaveState();
        var bits = ReadBits(n, signed);
        RestoreState();
        return bits;
    }

    public void ConsumeBits(int n)
    {
        for (var i = 0; i < n; i++)
            if (ReadBit() < 0) return;
    }

    private int ReadByte()
    {
        if (!stream.CanRead) return -1;
        return stream.ReadByte();
    }

    private int _savedBitBuffer = 0;
    private int _savedBitCount = 0;
    private long _savedPosition = 0;

    private void SaveState()
    {
        _savedBitBuffer = BitBuffer;
        _savedBitCount = BitCount;
        _savedPosition = stream.Position;
    }

    private void RestoreState()
    {
        BitBuffer = _savedBitBuffer;
        BitCount = _savedBitCount;
        stream.Position = _savedPosition;
    }
}
