using BenchmarkDotNet.Attributes;
using Image.Otp.Extensions;
using Image.Otp.Primitives;
using System.Drawing.Imaging;
using System.Drawing;

namespace Image.Otp.Console.Benchmarks;

[MemoryDiagnoser]
public class JpegLoadBenchmark
{
    private const string JpegBaseLine = "C:\\Users\\User\\source\\repos\\images\\firstJpg.jpg";

    [Benchmark]
    public int LoadJpegArray()
    {
        var image = ImageExtensions.LoadJpegBase<Rgba32>(JpegBaseLine);
        var size = image.Width * image.Height;
        image.Dispose();
        return size;
    }

    [Benchmark]
    public int LoadJpegMemory()
    {
        var image = ImageExtensions.LoadJpegMemory<Rgba32>(JpegBaseLine);
        var size = image.Width * image.Height;
        image.Dispose();
        return size;
    }

    [Benchmark]
    public int LoadJpegNative()
    {
        var image = ImageExtensions.LoadJpegNative<Rgba32>(JpegBaseLine);
        var size = image.Width * image.Height;
        image.Dispose();
        return size;
    }

    [Benchmark]
    public int LoadJpegImageSharp()
    {
        using var image = SixLabors.ImageSharp.Image.Load<SixLabors.ImageSharp.PixelFormats.Rgba32>(JpegBaseLine);
        var size = image.Width * image.Height;
        image.Dispose();
        return size;
    }

    [Benchmark]
    public int LoadJpegImageSharpMemoryStream()
    {
        var bytes = File.ReadAllBytes(JpegBaseLine);
        using var stream = new MemoryStream(bytes);
        using var image = SixLabors.ImageSharp.Image.Load<SixLabors.ImageSharp.PixelFormats.Rgba32>(stream);
        var size = image.Width * image.Height;
        image.Dispose();
        return size;
    }

    [Benchmark]
    public int LoadJpegImageSharpStream()
    {
        using var stream = new FileStream(JpegBaseLine, FileMode.Open);
        using var image = SixLabors.ImageSharp.Image.Load<SixLabors.ImageSharp.PixelFormats.Rgba32>(stream);
        var size = image.Width * image.Height;
        image.Dispose();
        return size;
    }

    private static readonly JpgProcessor<Rgba32> JpegProcessor = new JpgProcessor<Rgba32>();

    [Benchmark]
    public int LoadJpegLatest()
    {
        var image = JpegProcessor.Process("C:\\Users\\User\\source\\repos\\images\\firstJpg.jpg");
        var size = image.Width * image.Height;
        image.Dispose();
        return size;
    }
}
