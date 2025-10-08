using Image.Otp.Abstractions;
using Image.Otp.Core.Primitives;
using System.Numerics;

namespace Image.Otp.Core.Pixels;

public unsafe class Rgba32Processor : IPixelProcessor<Rgba32>
{
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

    public void FromYCbCr(ReadOnlySpan<byte> y, ReadOnlySpan<byte> cb, ReadOnlySpan<byte> cr, Span<Rgb24> output)
    {
        var const128 = new Vector<int>(128);
        var zeroInt = Vector<int>.Zero;
        var Yd = new Vector<int>(y);
        var Cbd = cb.IsEmpty ? zeroInt : new Vector<int>(cb) - const128;
        var Crd = cr.IsEmpty ? zeroInt : new Vector<int>(cr) - const128;

        var yFloat = Vector.ConvertToSingle(Yd);
        var cbFloat = Vector.ConvertToSingle(Cbd);
        var crFloat = Vector.ConvertToSingle(Crd);

        // Calculate RGB components
        var r = yFloat + 1.402f * crFloat;
        var g = yFloat - 0.344136f * cbFloat - 0.714136f * crFloat;
        var b = yFloat + 1.772f * cbFloat;

        // Clamp and convert back to bytes
        var zero = Vector<float>.Zero;
        var maxColor = new Vector<float>(255);

        var rClamped = Vector.Min(Vector.Max(r, zero), maxColor);
        var gClamped = Vector.Min(Vector.Max(g, zero), maxColor);
        var bClamped = Vector.Min(Vector.Max(b, zero), maxColor);
        
        var count = Vector<float>.Count;
        for (int i = 0; i < count; i++)
        {
            output[i] = new Rgb24(
                (byte)rClamped[i],
                (byte)gClamped[i],
                (byte)bClamped[i]
            );
        }
    }

    public void ProcessPixel(byte* srcPtr, int srcPos, Rgb24* dstPtr, int dstPos, int bytesPerPixel)
    {
        byte r = srcPtr[srcPos + 0];
        byte g = srcPtr[srcPos + 1];
        byte b = srcPtr[srcPos + 2];

        dstPtr[dstPos] = new Rgb24(r, g, b);
    }
}