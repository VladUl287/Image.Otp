using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Image.Otp.Abstractions;

public unsafe ref struct Image<T> : IDisposable where T : unmanaged, IPixel<T>
{
    private readonly T* _buffer;
    private readonly int _length;

    private bool _disposed;

    public readonly int Width;
    public readonly int Height;

    public readonly Span<T> Pixels
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get
        {
            if (_disposed) throw new ObjectDisposedException(nameof(Image<T>));
            return new Span<T>(_buffer, _length);
        }
    }

    public Image(int width, int height, nuint alignment = 16)
    {
        Width = width;
        Height = height;
        _length = width * height;
        _buffer = (T*)NativeMemory.AllocZeroed((nuint)(_length * sizeof(T)), alignment);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly ref T GetPixel(int x, int y)
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(Image<T>));

        var index = y * Width + x;
        if (index < 0 || index >= _length)
            throw new IndexOutOfRangeException();

        return ref _buffer[index];
    }

    public void Dispose()
    {
        if (_disposed) return;

        NativeMemory.Free(_buffer);
        _disposed = true;
    }
}