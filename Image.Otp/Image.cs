using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Image.Otp;

public readonly unsafe struct Image<T>(int width, int height) : IDisposable where T : unmanaged
{
    private readonly T[] _buffer = new T[width * height];

    public readonly int Width => width;
    public readonly int Height => height;

    public readonly Span<T> Pixels => _buffer.AsSpan(0, _buffer.Length);

    public ref T GetPixel(int x, int y) => ref _buffer[y * Width + x];

    public void Dispose() { }
}

public unsafe struct ImageOtp<T> : IDisposable where T : unmanaged
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
            if (_disposed) throw new ObjectDisposedException(nameof(ImageOtp<T>));
            return new Span<T>(_buffer, _length);
        }
    }

    public ImageOtp(int width, int height, nuint alignment = 16)
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
            throw new ObjectDisposedException(nameof(ImageOtp<T>));

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