using Image.Otp.Primitives;

namespace Image.Otp.Pixels;

public interface IPixel<T>
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

//public unsafe class DumpProcessor
//{
//    private static void L8Processor(byte* srcPtr, int srcPos, L8* dstPtr, int dstPos, int bytesPerPixel)
//    {
//        // Convert RGB to luminance: 0.299R + 0.587G + 0.114B
//        byte r = srcPtr[srcPos + 0];
//        byte g = srcPtr[srcPos + 1];
//        byte b = srcPtr[srcPos + 2];
//        byte luminance = (byte)((r * 0.299f + g * 0.587f + b * 0.114f));

//        dstPtr[dstPos] = new L8(luminance);
//    }

//    private static void L16Processor(byte* srcPtr, int srcPos, L16* dstPtr, int dstPos, int bytesPerPixel)
//    {
//        // 16-bit luminance
//        byte r = srcPtr[srcPos + 0];
//        byte g = srcPtr[srcPos + 1];
//        byte b = srcPtr[srcPos + 2];
//        ushort luminance = (ushort)((r * 0.299f + g * 0.587f + b * 0.114f) * 256);

//        dstPtr[dstPos] = new L16(luminance);
//    }

//    private static void La16Processor(byte* srcPtr, int srcPos, La16* dstPtr, int dstPos, int bytesPerPixel)
//    {
//        byte r = srcPtr[srcPos + 0];
//        byte g = srcPtr[srcPos + 1];
//        byte b = srcPtr[srcPos + 2];
//        byte a = bytesPerPixel == 4 ? srcPtr[srcPos + 3] : (byte)255;
//        byte luminance = (byte)((r * 0.299f + g * 0.587f + b * 0.114f));

//        dstPtr[dstPos] = new La16(luminance, a);
//    }

//    private static void La32Processor(byte* srcPtr, int srcPos, La32* dstPtr, int dstPos, int bytesPerPixel)
//    {
//        byte r = srcPtr[srcPos + 0];
//        byte g = srcPtr[srcPos + 1];
//        byte b = srcPtr[srcPos + 2];
//        byte a = bytesPerPixel == 4 ? srcPtr[srcPos + 3] : (byte)255;
//        ushort luminance = (ushort)((r * 0.299f + g * 0.587f + b * 0.114f) * 256);

//        dstPtr[dstPos] = new La32(luminance, a);
//    }

//    private static void Bgra32Processor(byte* srcPtr, int srcPos, Bgra32* dstPtr, int dstPos, int bytesPerPixel)
//    {
//        dstPtr[dstPos] = new Bgra32(
//            srcPtr[srcPos + 2], // B
//            srcPtr[srcPos + 1], // G
//            srcPtr[srcPos + 0], // R
//            bytesPerPixel == 4 ? srcPtr[srcPos + 3] : (byte)255 // A
//        );
//    }

//    private static void Argb32Processor(byte* srcPtr, int srcPos, Argb32* dstPtr, int dstPos, int bytesPerPixel)
//    {
//        dstPtr[dstPos] = new Argb32(
//            bytesPerPixel == 4 ? srcPtr[srcPos + 3] : (byte)255, // A
//            srcPtr[srcPos + 0], // R
//            srcPtr[srcPos + 1], // G
//            srcPtr[srcPos + 2]  // B
//        );
//    }
//}