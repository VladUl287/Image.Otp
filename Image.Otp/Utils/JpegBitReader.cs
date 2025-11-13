using System.Runtime.CompilerServices;

namespace Image.Otp.Core.Utils;

public sealed class JpegBitReader(Stream stream) : IBitReader
{
    private int _bitBuffer = 0;
    private int _bitCount = 0;

    public int BitBuffer => _bitBuffer;
    public int BitCount => _bitCount;


    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int ReadBit()
    {
        if (_bitCount == 0)
        {
            if (!RefillBuffer()) return -1;
        }

        _bitCount--;
        return (_bitBuffer >> _bitCount) & 1;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private bool RefillBuffer()
    {
        int b = ReadByte();
        if (b < 0) return false;

        if (b == 0xFF)
        {
            int next = ReadByte();
            if (next < 0) return false;
            if (next != 0x00)
            {
                stream.Seek(-2, SeekOrigin.Current);
                return false;
            }
        }

        _bitBuffer = b;
        _bitCount = 8;
        return true;
    }

    public int ReadBits(int n, bool signed = true)
    {
        if (n <= 0 || n > 32) return -1;

        int result = 0;
        for (int i = 0; i < n; i++)
        {
            int bit = ReadBit();
            if (bit < 0) return -1;
            result = (result << 1) | bit;
        }

        if (signed)
        {
            if (n < 32 && (result & (1 << (n - 1))) != 0)
            {
                result |= -1 << n;
            }
        }

        return result;
    }

    public int PeekBits(int n, bool signed = true)
    {
        throw new NotImplementedException();
    }

    public void ConsumeBits(int n)
    {
        throw new NotImplementedException();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int ReadByte()
    {
        if (!stream.CanRead) return -1;
        return stream.ReadByte();
    }
}
