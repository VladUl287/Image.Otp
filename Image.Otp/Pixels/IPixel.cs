using Image.Otp.Primitives;

namespace Image.Otp.Pixels;

public interface IPixel<T> : IEquatable<T>
{
}

public unsafe interface IPixelProcessor<T> where T : unmanaged, IPixel<T>
{
    void ProcessPixel(byte* srcPtr, int srcPos, T* dstPtr, int dstPos, int bytesPerPixel);
}

public unsafe class Rgba32Processor : IPixelProcessor<Rgba32>
{
    public void ProcessPixel(byte* srcPtr, int srcPos, Rgba32* dstPtr, int dstPos, int bytesPerPixel)
    {
        byte r = srcPtr[srcPos + 0];
        byte g = srcPtr[srcPos + 1];
        byte b = srcPtr[srcPos + 2];
        byte a = bytesPerPixel == 4 ? srcPtr[srcPos + 3] : (byte)255;

        dstPtr[dstPos] = new Rgba32(r, g, b, a);
    }
}

public unsafe class Rgb24Processor : IPixelProcessor<Rgb24>
{
    public void ProcessPixel(byte* srcPtr, int srcPos, Rgb24* dstPtr, int dstPos, int bytesPerPixel)
    {
        byte r = srcPtr[srcPos + 0];
        byte g = srcPtr[srcPos + 1];
        byte b = srcPtr[srcPos + 2];

        dstPtr[dstPos] = new Rgb24(r, g, b);
    }
}