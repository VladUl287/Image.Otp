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
}
