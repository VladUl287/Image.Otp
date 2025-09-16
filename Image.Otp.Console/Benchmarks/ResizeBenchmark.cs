using BenchmarkDotNet.Attributes;
using Image.Otp.Extensions;
using Image.Otp.Primitives;

namespace Image.Otp.Console.Benchmarks;

[MemoryDiagnoser]
public class ResizeBenchmark
{
    public static Image<Rgba32> Image = ImageExtensions.Load<Rgba32>("C:\\Users\\User\\source\\repos\\images\\image1.bmp");

    [Benchmark]
    public void ResizeNearestNeighbor() => ResizeExtensions.ResizeNearestNeighbor(Image, 1080, 1024).Dispose();

    [Benchmark]
    public void ResizeNearestNeighborSimdV2() => ResizeExtensions.ResizeNearestNeighborSimd(Image, 1080, 1024).Dispose();

    [Benchmark]
    public void ResizeParallel() => ResizeExtensions.ResizeParallel(Image, 1080, 1024).Dispose();

    [Benchmark]
    public void ResizeNearestNeighborParallelSimd() => ResizeExtensions.ResizeNearestNeighborParallelSimd(Image, 1080, 1024).Dispose();
}
