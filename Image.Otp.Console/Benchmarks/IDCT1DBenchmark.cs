using BenchmarkDotNet.Attributes;
using Image.Otp.Core.Extensions;
using Image.Otp.Core.Helpers.Jpg;

namespace Image.Otp.Console.Benchmarks;

[MemoryDiagnoser]
public class IDCT1DBenchmark
{
    public static readonly ReadOnlyMemory<double> RowD = new double[] { -200, 0, 0, 0, 0, 0, 0, 0 };
    public static readonly ReadOnlyMemory<float> RowF = new float[] { -200, 0, 0, 0, 0, 0, 0, 0 };

    [Benchmark]
    public double Compute_RowD_Scalar()
    {
        Span<double> result = stackalloc double[8];
        for (var y = 0; y < 8; y++)
            JPEG_IDCT.IDCT1Dllm_64f(RowD.Span, 0, result, 0);
        return result[0];
    }

    [Benchmark]
    public float Compute_RowF_SIMD()
    {
        Span<float> result = stackalloc float[8];
        for (var y = 0; y < 8; y++)
            AVXIDCTOPT.IDCT1Dllm_32f_SIMD(RowF.Span, 0, result, 0);
        return result[0];
    }
}
