
namespace Image.Otp;

public sealed class ProgressiveBitReader
{
    private readonly byte[] _data;
    private int _pos;          // next byte index to read
    private int _bitBuffer;    // current byte being consumed
    private int _bitCount;     // remaining bits in bitBuffer (0..8)

    public ProgressiveBitReader(byte[] data)
    {
        _data = data ?? throw new ArgumentNullException(nameof(data));
        _pos = 0;
        _bitBuffer = 0;
        _bitCount = 0;
    }

    // Expose read-only position for diagnostics
    public int Position => _pos;

    public string DebugPosition()
    {
        return $"pos={_pos}, bitCount={_bitCount}, bitBuffer=0x{_bitBuffer:X2}";
    }

    public string PeekHex(int count)
    {
        int end = Math.Min(_pos + count, _data.Length);
        return string.Join(" ", _data.Skip(_pos).Take(end - _pos).Select(b => b.ToString("X2")));
    }

    // Consume one physical byte and advance. Return -1 on EOF.
    private int ReadByteInternal()
    {
        if (_pos >= _data.Length) return -1;
        return _data[_pos++];
    }

    // Peek next physical byte without advancing. Return -1 on EOF.
    public int PeekByte()
    {
        if (_pos >= _data.Length) return -1;
        return _data[_pos];
    }

    // Move read pointer back one byte. Use only when you just consumed a single byte and want to un-read it.
    public void UnreadByte()
    {
        if (_pos > 0) _pos--;
    }

    // Align to next byte boundary by clearing bit buffer.
    public void AlignToByte()
    {
        _bitBuffer = 0;
        _bitCount = 0;
    }

    // Read a single bit (MSB-first). Returns 0 or 1, or -1 if EOF or a marker was encountered.
    // When a marker (0xFF xx with xx != 0x00) is seen, this method does NOT consume marker bytes and returns -1.
    public int ReadBit()
    {
        if (_bitCount == 0)
        {
            int b = ReadByteInternal();
            if (b < 0) return -1;

            if (b == 0xFF)
            {
                int next = PeekByte();
                if (next < 0) return -1; // EOF after 0xFF
                if (next == 0x00)
                {
                    // stuffed byte: consume the 0x00 and treat 0xFF as data
                    ReadByteInternal(); // advance past the 0x00
                    // b remains 0xFF
                }
                else
                {
                    // It's a marker: put back the 0xFF (we advanced once), do not consume marker here
                    UnreadByte(); // step pos back 1
                    return -1;    // signal marker encountered
                }
            }

            _bitBuffer = b & 0xFF;
            _bitCount = 8;
        }

        _bitCount--;
        return (_bitBuffer >> _bitCount) & 1;
    }

    // Read n bits (MSB-first), 0 <= n <= 16. Returns value or -1 on EOF/marker.
    public int ReadBits(int n)
    {
        if (n < 0) throw new ArgumentOutOfRangeException(nameof(n));
        if (n == 0) return 0;
        int v = 0;
        for (int i = 0; i < n; i++)
        {
            int b = ReadBit();
            if (b < 0) return -1;
            v = (v << 1) | b;
        }
        return v;
    }

    // Read a single byte-aligned physical byte (used for reading markers or other byte-level fields).
    // Aligns first, then returns next byte (or -1).
    public int ReadRawByte()
    {
        AlignToByte();
        return ReadByteInternal();
    }

    // Try to read a marker (0xFF xx). Caller should be at byte boundary before calling.
    // Returns marker value 0xFF00 | markerByte (e.g. 0xFFD8), or -1 if no marker present or EOF.
    // Does NOT consume marker if none present. If marker present it consumes it.
    public int ReadMarker()
    {
        AlignToByte();
        int b = PeekByte();
        if (b != 0xFF) return -1;
        // consume 0xFF
        ReadByteInternal();
        int marker = ReadByteInternal();
        if (marker < 0) return -1;
        return (0xFF << 8) | marker;
    }
}