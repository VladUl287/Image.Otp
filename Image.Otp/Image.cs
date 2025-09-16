using System.Buffers;

namespace Image.Otp;

public readonly unsafe struct Image<T>(int width, int height) : IDisposable where T : unmanaged
{
    private readonly T[] _buffer = new T[width * height];

    public readonly int Width => width;
    public readonly int Height => height;

    public readonly Span<T> Pixels => _buffer.AsSpan(0, _buffer.Length);

    public ref T GetPixel(int x, int y) => ref _buffer[y * Width + x];

    public void Dispose() => ArrayPool<T>.Shared.Return(_buffer);
}