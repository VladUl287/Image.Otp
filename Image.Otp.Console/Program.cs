using BenchmarkDotNet.Running;
using Image.Otp;
using Image.Otp.Console.Benchmarks;
using Image.Otp.Extensions;
using Image.Otp.Primitives;

BenchmarkRunner.Run<JpegLoadBenchmark>();
return;

//using var test = ImageExtensions.LoadJpegBase<Rgba32>("C:\\Users\\User\\source\\repos\\images\\firstJpg.jpg");
//using var test2 = ImageExtensions.LoadJpegMemory<Rgba32>("C:\\Users\\User\\source\\repos\\images\\firstJpg.jpg");
using var test3 = ImageExtensions.LoadJpegNative<Rgba32>("C:\\Users\\User\\source\\repos\\images\\firstJpg.jpg");

//test.SaveAsBmp("C:\\Users\\User\\source\\repos\\images\\1.bmp");
//test2.SaveAsBmp("C:\\Users\\User\\source\\repos\\images\\2.bmp");
//test3.SaveAsBmp("C:\\Users\\User\\source\\repos\\images\\3.bmp");

