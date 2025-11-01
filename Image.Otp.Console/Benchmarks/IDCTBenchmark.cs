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
    public double[] BlockD { 
        get
        {
            var result = new double[64];
            Array.Copy(_blockD, result, 64);
            return result;
        } 
    }
    public float[] BlockF { 
        get
        {
            var result = new float[64];
            Array.Copy(_blockF, result, 64);
            return result;
        } 
    }

    [Benchmark]
    public double Compute_BlockD_Scalar()
    {
        Span<double> span = BlockD;
        JPEG_IDCT.IDCT2D_llm_In_Place(span);
        return span[0];
    }

    [Benchmark]
    public float Compute_BlockF_Sse()
    {
        Span<float> span = BlockF;
        AVXIDCTOPT.IDCT2D_SIMD_SSE(span);
        return span[0];
    }

    [Benchmark]
    public float Compute_BlockF_FourRows()
    {
        Span<float> span = BlockF;
        AVXIDCTOPT.IDCT2D_SIMD_FOUR_ROWS(span);
        return span[0];
    }
}
