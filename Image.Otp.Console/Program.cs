using Image.Otp.Core.Extensions;
using Image.Otp.Core.Primitives;

//BenchmarkRunner.Run<JpegLoadBenchmark>();
//return;

var image = ImageExtensions.Load<Rgb24>("C:\\Users\\User\\source\\repos\\images\\firstJpg.jpg");

//var processor = new JpgProcessor<Rgb24>();
//var image = processor.Process("C:\\Users\\User\\source\\repos\\images\\firstJpg.jpg");

image.SaveAsBmp("C:\\Users\\User\\source\\repos\\images\\latest.jpg");
image.Dispose();

