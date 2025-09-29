using Image.Otp.Core.Primitives;

namespace Image.Otp.Core.Pixels;

public interface IPixel<T> : IEquatable<T>
{ }

public unsafe interface IPixelProcessor<T> where T : unmanaged, IPixel<T>
{
    void ProcessPixel(byte* srcPtr, int srcPos, T* dstPtr, int dstPos, int bytesPerPixel);

    T FromYCbCr(byte y, byte cb, byte cr);
}

public unsafe class Rgba32Processor : IPixelProcessor<Rgba32>
{
    public unsafe void FromRgba32(byte* srcPtr, Rgba32* dstPtr, int srcPos, int dstPos)
    {
        throw new NotImplementedException();
    }

    public Rgba32 FromYCbCr(byte y, byte cb, byte cr)
    {
        double Yd = y;
        double Cbd = cb - 128.0;
        double Crd = cr - 128.0;

        int r = (int)Math.Round(Yd + 1.402 * Crd);
        int g = (int)Math.Round(Yd - 0.344136 * Cbd - 0.714136 * Crd);
        int b = (int)Math.Round(Yd + 1.772 * Cbd);

        r = Math.Clamp(r, 0, 255);
        g = Math.Clamp(g, 0, 255);
        b = Math.Clamp(b, 0, 255);

        return new Rgba32((byte)r, (byte)g, (byte)b);
    }

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
    public Rgb24 FromYCbCr(byte y, byte cb, byte cr)
    {
        double Yd = y;
        double Cbd = cb - 128.0;
        double Crd = cr - 128.0;

        int r = (int)Math.Round(Yd + 1.402 * Crd);
        int g = (int)Math.Round(Yd - 0.344136 * Cbd - 0.714136 * Crd);
        int b = (int)Math.Round(Yd + 1.772 * Cbd);

        r = Math.Clamp(r, 0, 255);
        g = Math.Clamp(g, 0, 255);
        b = Math.Clamp(b, 0, 255);

        return new Rgb24((byte)r, (byte)g, (byte)b);
    }

    public void ProcessPixel(byte* srcPtr, int srcPos, Rgb24* dstPtr, int dstPos, int bytesPerPixel)
    {
        byte r = srcPtr[srcPos + 0];
        byte g = srcPtr[srcPos + 1];
        byte b = srcPtr[srcPos + 2];

        dstPtr[dstPos] = new Rgb24(r, g, b);
    }
}