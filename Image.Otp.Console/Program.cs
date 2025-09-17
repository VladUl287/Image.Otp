using BenchmarkDotNet.Running;
using Image.Otp.Console.Benchmarks;
using Image.Otp.Extensions;
using Image.Otp.Primitives;

//BenchmarkRunner.Run<JpegLoadBenchmark>();
//return;

var test = ImageExtensions.LoadJpegBase<Rgba32>("C:\\Users\\User\\source\\repos\\images\\firstJpg.jpg");
var test2 = ImageExtensions.LoadJpegMemory<Rgba32>("C:\\Users\\User\\source\\repos\\images\\firstJpg.jpg");

return;

//test.SaveAsBmp("C:\\Users\\User\\source\\repos\\images\\progressive.bmp");
