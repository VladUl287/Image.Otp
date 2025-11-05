using BenchmarkDotNet.Running;
using Image.Otp.Abstractions;
using Image.Otp.Console.Benchmarks;
using Image.Otp.Core.Constants;
using Image.Otp.Core.Extensions;
using Image.Otp.Core.Formats;
using Image.Otp.Core.Loaders;
using Image.Otp.Core.Primitives;

BenchmarkRunner.Run<IDCTBenchmark>();
return;

var formatResovler = new BaseFormatResolver();
var loaders = new IImageLoader[] { new JpegLoader(), new BmpLoader() };
var loader = new ImageLoader(formatResovler, loaders);

var filePath = "C:\\Users\\User\\source\\repos\\images\\firstJpg.jpg";
var outputPath = "C:\\Users\\User\\source\\repos\\images\\firstJpg-output.jpg";

var image = loader.Load<Rgb24>(filePath);
image.SaveAsBmp(outputPath);
image.Dispose();

//var bytes = File.ReadAllBytes(filePath);
//var image = ImageExtensions.LoadJpeg<Rgba32>(bytes);
//image.SaveAsBmp(outputPath);
//image.Dispose();