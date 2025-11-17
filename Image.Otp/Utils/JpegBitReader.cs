using Image.Otp.Core.Constants;
using System.Runtime.CompilerServices;

namespace Image.Otp.Core.Utils;

public sealed class JpegBitReader(Stream stream)
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
        var b = ReadByte();
        if (b < 0) return false;

        if (b == 0xFF)
        {
            var next = ReadByte();
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

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private bool FillBuffer()
    {
        var b = ReadByte();
        if (b < 0) return false;

        if (b == 0xFF)
        {
            var next = ReadByte();
            if (next < 0) return false;
            if (next != 0x00)
            {
                stream.Seek(-2, SeekOrigin.Current);
                return false;
            }
        }

        _bitBuffer = (_bitBuffer << 8) | b;
        _bitCount += 8;
        return true;
    }

    public int ReadBits(int n)
    {
        if (n <= 0 || n > 32) return -1;

        var result = 0;
        for (var i = 0; i < n; i++)
        {
            var bit = ReadBit();
            if (bit < 0) return -1;
            result = (result << 1) | bit;
        }

        return result;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool EnsureBits(int minBits = Huffman.MinBits)
    {
        while (_bitCount < minBits)
            if (!FillBuffer()) return false;
        return true;
    }

    public int PeekBits(int n)
    {
        if (n <= 0 || n > 32) return -1;

        if (_bitCount < n)
            return -1;

        return (_bitBuffer >> (_bitCount - n)) & ((1 << n) - 1);

    }

    public void ConsumeBits(int n)
    {
        if (n < 0 || n > _bitCount)
            throw new ArgumentOutOfRangeException(nameof(n), $"Cannot consume {n} bits when only {_bitCount} are available");

        _bitCount -= n;
        _bitBuffer &= (1 << _bitCount) - 1;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int ReadByte()
    {
        if (!stream.CanRead) return -1;
        return stream.ReadByte();
    }
}
