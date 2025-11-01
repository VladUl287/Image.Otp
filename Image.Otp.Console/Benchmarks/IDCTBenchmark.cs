using BenchmarkDotNet.Attributes;
using Image.Otp.Core.Extensions;
using Image.Otp.Core.Helpers.Jpg;

namespace Image.Otp.Console.Benchmarks;

[MemoryDiagnoser]
public class IDCTBenchmark
{
    public static readonly double[] BlockD = new double[] {
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
    public float[] BlockF { 
        get
        {
            var result = new float[64];
            Array.Copy(_blockF, result, 64);
            return result;
        } 
    }

    //[Benchmark]
    public double Compute_BlockD_Scalar()
    {
        Span<double> span = [.. BlockD];
        JPEG_IDCT.IDCT2D_llm_In_Place(span);
        return span[0];
    }

    //[Benchmark]
    public float Compute_BlockF_Sse()
    {
        Span<float> span = [.. BlockF];
        AVXIDCTOPT.IDCT2D_SIMD(span);
        return span[0];
    }

    [Benchmark]
    public float Compute_BlockF_Avx()
    {
        Span<float> span = BlockF;
        //AVXIDCTOPT.IDCT2D_SIMD_G(span);
        return span[0];
    }
}
