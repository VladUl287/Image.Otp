namespace Image.Otp.Models.Jpeg;

public sealed class ComponentInfo
{
    public byte Id { get; init; }
    public byte SamplingFactor { get; init; } 
    public byte QuantizationTableId { get; init; }
    public byte HorizontalSampling { get; init; }
    public byte VerticalSampling { get; init; }
}

public sealed class FrameInfo
{
    public byte Precision { get; init; }
    public int Width { get; init; }
    public int Height { get; init; }
    public ComponentInfo[] Components { get; init; } = [];
}
