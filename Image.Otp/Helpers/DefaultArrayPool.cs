using Image.Otp.Abstractions;
using System.Buffers;
using System.Runtime.CompilerServices;

namespace Image.Otp.Core.Helpers;

public readonly struct DefaultArrayPool<T> : IArrayPool<T>
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly T[] Rent(int size) => ArrayPool<T>.Shared.Rent(size);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly void Return(T[] values) => ArrayPool<T>.Shared.Return(values);
}
