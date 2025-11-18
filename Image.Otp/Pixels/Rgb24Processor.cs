using Image.Otp.Abstractions;
using Image.Otp.Core.Primitives;
using System.Collections.Concurrent;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;

namespace Image.Otp.Core.Pixels;

public unsafe class Rgb24Processor : IPixelProcessor<Rgb24>
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Rgb24 YCbCrToRgb24(byte y, byte cb, byte cr)
    {
        double Yd = y;
        double Cbd = cb - 128.0;
        double Crd = cr - 128.0;

        var r = (int)Math.Round(Yd + 1.402 * Crd);
        var g = (int)Math.Round(Yd - 0.344136 * Cbd - 0.714136 * Crd);
        var b = (int)Math.Round(Yd + 1.772 * Cbd);

        r = Math.Clamp(r, 0, 255);
        g = Math.Clamp(g, 0, 255);
        b = Math.Clamp(b, 0, 255);

        return new Rgb24((byte)r, (byte)g, (byte)b);
    }

    public Rgb24 FromYCbCr(byte y, byte cb, byte cr) => YCbCrToRgb24(y, cb, cr);

    public void FromYCbCr(float* y, float* cb, float* cr, Span<Rgb24> output)
    {
        var i = 0;

        if (Avx.IsSupported)
        {
            //fixed (Rgb24* outputPtr = output)
            //{
            //    FromYCbCrParallel(y, cb, cr, outputPtr, output.Length);
            //}

            var zero = Vector256.Create(0f);
            var c128 = Vector256.Create(128f);
            var maxColor = Vector256.Create(255f);
            var f1_402 = Vector256.Create(1.402f);
            var f1_772 = Vector256.Create(1.772f);
            var f0_344 = Vector256.Create(-0.344136f);
            var f0_714 = Vector256.Create(-0.714136f);

            var vecCount = Vector256<float>.Count;

            for (; i < output.Length - vecCount; i += vecCount)
            {
                var yVec = Vector256.Load(y + i);
                var cbVec = Vector256.Load(cb + i);
                var crVec = Vector256.Load(cr + i);

                yVec = ClampToByte(yVec);
                cbVec = ClampToByte(cbVec);
                crVec = ClampToByte(crVec);

                cbVec = Avx.Subtract(cbVec, c128);
                crVec = Avx.Subtract(crVec, c128);

                var rFloat = Avx.Add(yVec, Avx.Multiply(crVec, f1_402));
                var bFloat = Avx.Add(yVec, Avx.Multiply(cbVec, f1_772));
                var gFloat = Avx.Add(yVec, Avx.Add(
                    Avx.Multiply(cbVec, f0_344),
                    Avx.Multiply(crVec, f0_714)));

                rFloat = Avx.Min(Avx.Max(rFloat, zero), maxColor);
                gFloat = Avx.Min(Avx.Max(gFloat, zero), maxColor);
                bFloat = Avx.Min(Avx.Max(bFloat, zero), maxColor);

                for (int j = 0; j < vecCount; j++)
                {
                    var r = (byte)rFloat.GetElement(j);
                    var g = (byte)gFloat.GetElement(j);
                    var b = (byte)bFloat.GetElement(j);
                    output[j + i] = new Rgb24(r, g, b);
                }
            }
        }

        for (; i < output.Length; i++)
            output[i] = FromYCbCr(ClampToByte(y[i]), ClampToByte(cb[i]), ClampToByte(cr[i]));
    }

    private static readonly ParallelOptions parallelOptions = new() { MaxDegreeOfParallelism = Environment.ProcessorCount };
    private static void FromYCbCrParallel(float* y, float* cb, float* cr, Rgb24* output, int length)
    {
        var partitions = Partitioner.Create(0, length, rangeSize: 5000);
        Parallel.ForEach(partitions, parallelOptions, (part) =>
        {
            var zero = Vector256.Create(0f);
            var c128 = Vector256.Create(128f);
            var maxColor = Vector256.Create(255f);
            var f1_402 = Vector256.Create(1.402f);
            var f1_772 = Vector256.Create(1.772f);
            var f0_344 = Vector256.Create(-0.344136f);
            var f0_714 = Vector256.Create(-0.714136f);

            var vecCount = Vector256<float>.Count;

            var i = part.Item1;
            for (; i < part.Item2 - vecCount; i += vecCount)
            {
                var yVec = Vector256.Load(y + i);
                var cbVec = Vector256.Load(cb + i);
                var crVec = Vector256.Load(cr + i);

                yVec = ClampToByte(yVec);
                cbVec = ClampToByte(cbVec);
                crVec = ClampToByte(crVec);

                cbVec = Avx.Subtract(cbVec, c128);
                crVec = Avx.Subtract(crVec, c128);

                var rFloat = Avx.Add(yVec, Avx.Multiply(crVec, f1_402));
                var bFloat = Avx.Add(yVec, Avx.Multiply(cbVec, f1_772));
                var gFloat = Avx.Add(yVec, Avx.Add(
                    Avx.Multiply(cbVec, f0_344),
                    Avx.Multiply(crVec, f0_714)));

                rFloat = Avx.Min(Avx.Max(rFloat, zero), maxColor);
                gFloat = Avx.Min(Avx.Max(gFloat, zero), maxColor);
                bFloat = Avx.Min(Avx.Max(bFloat, zero), maxColor);

                for (int j = 0; j < vecCount; j++)
                {
                    var r = (byte)rFloat.GetElement(j);
                    var g = (byte)gFloat.GetElement(j);
                    var b = (byte)bFloat.GetElement(j);
                    output[j + i] = new Rgb24(r, g, b);
                }
            }

            for (; i < part.Item2; i++)
                output[i] = YCbCrToRgb24(ClampToByte(y[i]), ClampToByte(cb[i]), ClampToByte(cr[i]));
        });
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static byte ClampToByte(float sample)
    {
        var value = sample + 128.0f;
        return (byte)Math.Max(0f, Math.Min(value, 255f));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector256<float> ClampToByte(Vector256<float> sample)
    {
        var value = Avx.Add(sample, Vector256.Create(128.0f));
        var zero = Vector256<float>.Zero;
        var max = Vector256.Create(255.0f);
        return Avx.Max(Avx.Min(value, max), zero);
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