﻿using Image.Otp.Abstractions;
using Image.Otp.Core.Primitives;
using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;

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

    public void FromYCbCr(byte* y, byte* cb, byte* cr, Span<Rgb24> output)
    {
        if (!Avx.IsSupported)
        {
            for (int j = 0; j < output.Length; j++)
                output[j] = FromYCbCr(y[j], cb[j], cr[j]);
            return;
        }

        var c128 = Vector256.Create(128f);

        var maxColor = Vector256.Create(255f);
        var zero = Vector256.Create(0f);

        var f1_402 = Vector256.Create(1.402f);
        var f0_344 = Vector256.Create(-0.344136f);
        var f0_714 = Vector256.Create(-0.714136f);
        var f1_772 = Vector256.Create(1.772f);

        var vectorCount = Vector256<float>.Count;

        var i = 0;
        //var byteSpan = MemoryMarshal.AsBytes(output);
        //fixed (byte* bytePtr = byteSpan)
        //{
        for (; i < output.Length - vectorCount; i += vectorCount)
        {
            // Load and convert to float
            var yVec = Avx.ConvertToVector256Single(Avx2.ConvertToVector256Int32(y + i));
            var cbVec = Avx.ConvertToVector256Single(Avx2.ConvertToVector256Int32(cb + i));
            var crVec = Avx.ConvertToVector256Single(Avx2.ConvertToVector256Int32(cr + i));

            cbVec = Avx.Subtract(cbVec, c128);
            crVec = Avx.Subtract(crVec, c128);

            // Calculate R, G, B
            var rFloat = Avx.Add(yVec, Avx.Multiply(crVec, f1_402));
            var bFloat = Avx.Add(yVec, Avx.Multiply(cbVec, f1_772));
            var gFloat = Avx.Add(yVec, Avx.Add(
                Avx.Multiply(cbVec, f0_344),
                Avx.Multiply(crVec, f0_714)));

            // Clamp to [0, 255]
            rFloat = Avx.Min(Avx.Max(rFloat, zero), maxColor);
            gFloat = Avx.Min(Avx.Max(gFloat, zero), maxColor);
            bFloat = Avx.Min(Avx.Max(bFloat, zero), maxColor);

            var rInt = Avx.ConvertToVector256Int32(rFloat);
            var gInt = Avx.ConvertToVector256Int32(gFloat);
            var bInt = Avx.ConvertToVector256Int32(bFloat);

            //var rgb16 = Avx2.PackUnsignedSaturate(
            //    Avx2.PackSignedSaturate(rInt, gInt),
            //    Avx2.PackSignedSaturate(bInt, Vector256<int>.Zero)
            //);

            //var lower = rgb16.GetLower();
            //var upper = rgb16.GetUpper();
            //StoreRgbPixels(bytePtr + i, lower, upper);

            for (int j = 0; j < vectorCount; j++)
            {
                var r = (byte)rInt.GetElement(j); // R
                var g = (byte)gInt.GetElement(j); // G  
                var b = (byte)bInt.GetElement(j); // B
                output[j + i] = new Rgb24(r, g, b);
            }
        }
        //}

        for (; i < output.Length; i++)
            output[i] = FromYCbCr(y[i], cb[i], cr[i]);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void StoreRgbPixels(byte* dest, Vector128<byte> lower, Vector128<byte> upper)
    {
        // Rearrange from RRRRGGGGBBBB to RGBRGBRGBRGB
        if (Ssse3.IsSupported)
        {
            // Use SSSE3 for better pixel packing if available
            var shuffleMask = Vector128.Create((byte)0, 4, 8, 1, 5, 9, 2, 6, 10, 3, 7, 11, 12, 13, 14, 15);

            var shuffledLower = Ssse3.Shuffle(lower, shuffleMask);
            var shuffledUpper = Ssse3.Shuffle(upper, shuffleMask);

            Sse2.Store(dest, shuffledLower);
            Sse2.Store(dest + 12, shuffledUpper);
        }
        else
        {
            for (int j = 0; j < 8; j++)
            {
                dest[j * 3] = lower.GetElement(j);     // R
                dest[j * 3 + 1] = lower.GetElement(j + 8); // G  
                dest[j * 3 + 2] = upper.GetElement(j);     // B
            }
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