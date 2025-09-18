using BenchmarkDotNet.Attributes;
using Image.Otp.Extensions;
using Image.Otp.Primitives;

namespace Image.Otp.Console.Benchmarks;

[MemoryDiagnoser]
public class JpegLoadBenchmark
{
    private const string JpegBaseLine = "C:\\Users\\User\\source\\repos\\images\\firstJpg.jpg";

    [Benchmark]
    public int LoadJpegArray()
    {
        var image = ImageExtensions.LoadJpegBase<Rgba32>(JpegBaseLine);
        var size = image.Width  * image.Height;
        image.Dispose();
        return size;
    }

    [Benchmark]
    public int LoadJpegMemory()
    {
        var image = ImageExtensions.LoadJpegMemory<Rgba32>(JpegBaseLine);
        var size = image.Width  * image.Height;
        image.Dispose();
        return size;
    }

    [Benchmark]
    public int LoadJpegNative()
    {
        var image = ImageExtensions.LoadJpegNative<Rgba32>(JpegBaseLine);
        var size = image.Width  * image.Height;
        image.Dispose();
        return size;
    }
}
