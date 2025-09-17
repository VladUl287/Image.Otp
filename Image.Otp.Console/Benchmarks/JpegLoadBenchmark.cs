using BenchmarkDotNet.Attributes;
using Image.Otp.Extensions;
using Image.Otp.Primitives;

namespace Image.Otp.Console.Benchmarks;

[MemoryDiagnoser]
public class JpegLoadBenchmark
{
    private const string JpegBaseLine = "C:\\Users\\User\\source\\repos\\images\\firstJpg-progressive.jpg";
    private const string JpegProgressive = "C:\\Users\\User\\source\\repos\\images\\firstJpg.jpg";

    [Benchmark]
    public int LoadJpegBaseLine()
    {
        var image = ImageExtensions.LoadJpegBase<Rgba32>(JpegBaseLine);
        var size = image.Width  * image.Height;
        image.Dispose();
        return size;
    }

    [Benchmark]
    public int LoadJpegProgressive()
    {
        var image = ImageExtensions.LoadJpegBase<Rgba32>(JpegProgressive);
        var size = image.Width  * image.Height;
        image.Dispose();
        return size;
    }
}
