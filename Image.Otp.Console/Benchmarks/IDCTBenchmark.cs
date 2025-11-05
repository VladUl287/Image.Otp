using BenchmarkDotNet.Attributes;
using Image.Otp.Core.Extensions;
using Image.Otp.Core.Helpers.Jpg;

namespace Image.Otp.Console.Benchmarks;

[MemoryDiagnoser]
public class IDCTBenchmark
{
    public static readonly double[] _blockD = new double[] {
        -200, 0, 0, 0, 0, 0, 0, 0,
        -7, 0, 0, 0, 0, 0, 0, 0,
        0, 0, 0, 0, 0, 0, 0, 0,
        0, 0, 0, 0, 0, 0, 0, 0,
        0, 0, 0, 0, 0, 0, 0, 0,
        0, 0, 0, 0, 0, 0, 0, 0,
        0, 0, 0, 0, 0, 0, 0, 0,
        0, 0, 0, 0, 0, 0, 0, 0,
    };
    public static readonly float[] _blockF = new float[] {
        -200, 0, 0, 0, 0, 0, 0, 0,
        -7, 0, 0, 0, 0, 0, 0, 0,
        0, 0, 0, 0, 0, 0, 0, 0,
        0, 0, 0, 0, 0, 0, 0, 0,
        0, 0, 0, 0, 0, 0, 0, 0,
        0, 0, 0, 0, 0, 0, 0, 0,
        0, 0, 0, 0, 0, 0, 0, 0,
        0, 0, 0, 0, 0, 0, 0, 0,
    };

    [Benchmark]
    public double Compute_BlockD_Scalar()
    {
        Span<double> span = _blockD;
        IDCT.IDCT2D_LLM(span);
        return span[0];
    }

    [Benchmark]
    public float Compute_BlockF_EightRows()
    {
        Span<float> span = _blockF;
        IDCT_AVX.IDCT2D_AVX(span);
        return span[0];
    }
}
