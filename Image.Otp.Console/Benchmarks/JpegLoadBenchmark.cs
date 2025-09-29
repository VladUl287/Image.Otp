using BenchmarkDotNet.Attributes;
using Image.Otp.Core.Formats;
using Image.Otp.Core.Loaders;
using Image.Otp.Core.Primitives;

namespace Image.Otp.Console.Benchmarks;

[MemoryDiagnoser]
public class JpegLoadBenchmark
{
    private const string JpegBaseLine = "C:\\Users\\User\\source\\repos\\images\\firstJpg.jpg";

    //[Benchmark]
    //public int LoadJpegArray()
    //{
    //    var image = ImageExtensions.LoadJpegBase<Rgba32>(JpegBaseLine);
    //    var size = image.Width * image.Height;
    //    image.Dispose();
    //    return size;
    //}

    //[Benchmark]
    //public int LoadJpegMemory()
    //{
    //    var image = ImageExtensions.LoadJpegMemory<Rgba32>(JpegBaseLine);
    //    var size = image.Width * image.Height;
    //    image.Dispose();
    //    return size;
    //}

    //[Benchmark]
    //public int LoadJpegNative()
    //{
    //    var image = ImageExtensions.LoadJpegNative<Rgba32>(JpegBaseLine);
    //    var size = image.Width * image.Height;
    //    image.Dispose();
    //    return size;
    //}

    //[Benchmark]
    //public int LoadJpegImageSharp()
    //{
    //    using var image = SixLabors.ImageSharp.Image.Load<SixLabors.ImageSharp.PixelFormats.Rgba32>(JpegBaseLine);
    //    var size = image.Width * image.Height;
    //    image.Dispose();
    //    return size;
    //}

    //[Benchmark]
    //public int LoadJpegImageSharpMemoryStream()
    //{
    //    var bytes = File.ReadAllBytes(JpegBaseLine);
    //    using var stream = new MemoryStream(bytes);
    //    using var image = SixLabors.ImageSharp.Image.Load<SixLabors.ImageSharp.PixelFormats.Rgba32>(stream);
    //    var size = image.Width * image.Height;
    //    image.Dispose();
    //    return size;
    //}

    [Benchmark]
    public int LoadJpegImageSharpStream()
    {
        using var stream = new FileStream(JpegBaseLine, FileMode.Open);
        using var image = SixLabors.ImageSharp.Image.Load<SixLabors.ImageSharp.PixelFormats.Rgba32>(stream);
        var size = image.Width * image.Height;
        image.Dispose();
        return size;
    }

    private readonly static ImageLoader uniLoader = new(new BaseFormatResolver(), [new JpegLoader()]);

    [Benchmark]
    public int LoadJpegUniversalLoader()
    {
        using var image = uniLoader.Load<Rgba32>(JpegBaseLine);
        var size = image.Width * image.Height;
        image.Dispose();
        return size;
    }

    private readonly static JpegLoader jpegLoader = new();

    [Benchmark]
    public int LoadJpegSpecificLoader()
    {
        using var image = jpegLoader.Load<Rgba32>(JpegBaseLine);
        var size = image.Width * image.Height;
        image.Dispose();
        return size;
    }
}
