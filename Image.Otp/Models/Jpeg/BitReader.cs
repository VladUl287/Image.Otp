namespace Image.Otp.Models.Jpeg;

public class BitReader
{
    private readonly byte[] data;
    private int pos;
    private uint bitBuf = 0;
    private int bitCount = 0;

    public int BytePosition => pos;
    public int BitCount => bitCount;

    public BitReader(byte[] data, int startPos = 0)
    {
        this.data = data ?? throw new ArgumentNullException(nameof(data));
        this.pos = startPos;
    }

    // In BitReader
    private bool lastWasMarker;
    public bool LastWasMarker => lastWasMarker;

    private bool RefillOnce()
    {
        lastWasMarker = false;
        if (pos >= data.Length) return false;
        int b = data[pos++];
        if (b == 0xFF)
        {
            if (pos < data.Length && data[pos] == 0x00)
            {
                pos++;
            }
            else
            {
                pos--;
                lastWasMarker = true;
                return false;
            }
        }
        bitBuf = (bitBuf << 8) | (uint)b;
        bitCount += 8;
        return true;
    }


    // Ensure at least n bits available; returns true if satisfied, false on EOF/marker
    private bool EnsureBits(int n)
    {
        while (bitCount < n)
        {
            if (!RefillOnce()) return false;
        }
        return true;
    }

    // Read up to 16 bits MSB-first. Returns -1 on EOF/marker.
    public int ReadBits(int n)
    {
        if (n == 0) return 0;
        if (n < 0 || n > 16) throw new ArgumentOutOfRangeException(nameof(n));
        while (bitCount < n)
        {
            if (!RefillOnce())
            {
                if (lastWasMarker) return -2; // marker encountered

                return -1; // EOF
            }
        }

        if (!EnsureBits(n)) return -1;

        int shift = bitCount - n;
        uint mask = (uint)((1 << n) - 1);
        int val = (int)((bitBuf >> shift) & mask);

        // Keep lower 'shift' bits
        if (shift > 0)
            bitBuf &= (uint)((1u << shift) - 1u);
        else
            bitBuf = 0;

        bitCount = shift;
        return val;
    }

    public int ReadBit() => ReadBits(1);

    // Align to next byte boundary by dropping leftover bits (not zeroing arbitrarily)
    public void AlignToByte()
    {
        int rem = bitCount % 8;
        if (rem != 0)
        {
            // Drop the rem high bits
            ReadBits(rem);
        }
    }

    // Read next raw byte from the stream (used for markers). Returns -1 on EOF, or -2 if marker encountered (pos left at marker start).
    public int ReadRawByteOrMinusOne()
    {
        if (pos >= data.Length) return -1;
        int b = data[pos++];

        if (b == 0xFF)
        {
            if (pos >= data.Length) return -1;
            int next = data[pos];
            if (next == 0x00)
            {
                pos++; // consume stuffing
                return 0xFF;
            }
            else
            {
                // Found marker; step back so caller can read marker bytes (0xFF then next)
                pos--;
                return -2;
            }
        }
        return b;
    }

    public int GetBytePosition() => pos;
    public int GetBitPosition() => bitCount;
}


//public sealed class BitReader(byte[] data)
//{
//    private readonly byte[] _data = data ?? throw new ArgumentNullException(nameof(data));
//    private int _bytePos = 0;
//    private int _bitBuffer = 0;
//    private int _bitCount = 0;
//    private readonly int _length = data.Length;

//    private int ReadRawByteOrMinusOne()
//    {
//        if (_bytePos >= _length) return -1;
//        int b = _data[_bytePos++];

//        if (b == 0xFF)
//        {
//            if (_bytePos >= _length)
//                return -1;

//            int next = _data[_bytePos];
//            if (next == 0x00)
//            {
//                _bytePos++; // Consume stuffed byte
//                return 0xFF;
//            }
//            else
//            {
//                // Marker found - handle or error out
//                throw new InvalidOperationException(
//                    $"Unexpected marker 0xFF{next:X2} at position {_bytePos - 1}");
//            }
//        }

//        return b;
//    }

//    private void FillBuffer(int n)
//    {
//        while (_bitCount < n)
//        {
//            int next = ReadRawByteOrMinusOne();
//            if (next < 0) break;
//            _bitBuffer = (_bitBuffer << 8) | next;
//            _bitCount += 8;
//        }
//    }

//    public int ReadBit()
//    {
//        FillBuffer(1);
//        if (_bitCount == 0) return -1;

//        _bitCount--;
//        int bit = (_bitBuffer >> _bitCount) & 1;
//        return bit;
//    }

//    public int ReadBits(int n)
//    {
//        if (n <= 0) return 0;
//        FillBuffer(n);
//        if (_bitCount < n) return -1;

//        _bitCount -= n;
//        int val = (_bitBuffer >> _bitCount) & ((1 << n) - 1);
//        return val;
//    }

//    public void AlignToByte()
//    {
//        if (_bitCount > 0)
//        {
//            _bitCount = 0;
//            _bitBuffer = 0;
//        }
//    }

//    public int ReadByte()
//    {
//        // If we're byte-aligned, read directly
//        if (_bitCount == 0)
//            return ReadRawByteOrMinusOne();

//        return ReadBits(8);
//    }
//}