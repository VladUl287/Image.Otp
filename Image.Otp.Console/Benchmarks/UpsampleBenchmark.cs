using BenchmarkDotNet.Attributes;
using Image.Otp.Core.Extensions;

namespace Image.Otp.Console.Benchmarks;

[MemoryDiagnoser]
public class UpsampleBenchmark
{
    private float[] _blockData = [];
    private byte[] _outputBuffer = [];

    //[Params(8, 16, 32)]
    public int BlockSize { get; set; } = 8;

    //[Params(2, 4, 8)]
    public int ScaleFactor { get; set; } = 2;

    [GlobalSetup]
    public void Setup()
    {
        var blockLength = BlockSize * BlockSize;
        _blockData = new float[blockLength];
        _outputBuffer = new byte[blockLength * ScaleFactor * ScaleFactor];

        var random = new Random(42);
        for (var i = 0; i < _blockData.Length; i++)
            _blockData[i] = (float)(random.NextDouble() * 255.0);
    }

    [Benchmark]
    public void Upsample_8x8_Scale2x()
    {
        _blockData.AsSpan().UpsampleInPlace(_outputBuffer, 8, 8, 2, 2, 0, 0);
    }

    //[Benchmark]
    public void Upsample_8x8_Scale4x()
    {
        _blockData.AsSpan().UpsampleInPlace(_outputBuffer, 8, 8, 4, 4, 0, 0);
    }

    //[Benchmark]
    public void Upsample_16x16_Scale2x()
    {
        _blockData.AsSpan().UpsampleInPlace(_outputBuffer, 16, 16, 2, 2, 0, 0);
    }

    //[Benchmark]
    public void Upsample_16x16_Scale4x()
    {
        _blockData.AsSpan().UpsampleInPlace(_outputBuffer, 16, 16, 4, 4, 0, 0);
    }
}
