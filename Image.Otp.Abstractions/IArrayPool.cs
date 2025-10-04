namespace Image.Otp.Abstractions;

public interface IArrayPool<T>
{
    T[] Rent(int size);
    void Return(T[] values);
}
