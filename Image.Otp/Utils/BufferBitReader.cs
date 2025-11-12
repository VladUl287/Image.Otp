using System.Runtime.InteropServices;

namespace Image.Otp.Core.Utils;

public unsafe sealed class BufferBitReader : IDisposable
{
    private readonly Stream _stream;
    private readonly byte* _buffer;

    public BufferBitReader(Stream stream, int bufferSize = 4096)
    {
        _stream = stream;

        var byteCount = (nuint)Math.Min(bufferSize, _stream.Length);
        _buffer = (byte*)NativeMemory.Alloc(byteCount);
    }

    public int BitBuffer { get; private set; } = 0;
    public int BitCount { get; private set; } = 0;

    private byte ReadByte()
    {
        return 1;
    }

    public int ReadBit()
    {
        return 1;
    }

    public void Dispose()
    {
        NativeMemory.Free(_buffer);
    }
}
