using BenchmarkDotNet.Running;
using Image.Otp.Abstractions;
using Image.Otp.Console.Benchmarks;
using Image.Otp.Core.Extensions;
using Image.Otp.Core.Formats;
using Image.Otp.Core.Loaders;
using Image.Otp.Core.Primitives;

var filePath = "C:\\Users\\User\\source\\repos\\images\\firstJpg.jpg";
var outputPath = "C:\\Users\\User\\source\\repos\\images\\firstJpg-output.jpg";

if (IsReleaseBuild())
{
    //BenchmarkRunner.Run<UpsampleBenchmark>();
    //BenchmarkRunner.Run<IDCTBenchmark>();
    BenchmarkRunner.Run<JpegLoadBenchmark>();
    return;
}

//var bytes = File.ReadAllBytes(filePath);
//var image = ImageExtensions.LoadJpeg<Rgba32>(bytes);
//image.SaveAsBmp(outputPath);
//image.Dispose();

var formatResovler = new BaseFormatResolver();
var loaders = new IImageLoader[] { new JpegLoader(), new BmpLoader() };
var loader = new ImageLoader(formatResovler, loaders);

var images = loader.Load<Rgb24>(filePath);
images.SaveAsBmp(outputPath);
images.Dispose();

static bool IsReleaseBuild()
{
#if DEBUG
    return false;
#else
        return true;
#endif
}