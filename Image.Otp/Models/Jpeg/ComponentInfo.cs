namespace Image.Otp.Models.Jpeg;

public class ComponentInfo
{
    public byte Id { get; set; }
    public byte SamplingFactor { get; set; }  // Combined horizontal and vertical factors (H << 4) | V
    public byte QuantizationTableId { get; set; }
    public byte HorizontalSampling { get; set; }
    public byte VerticalSampling { get; set; }
}

public class FrameInfo
{
    public byte Precision { get; set; }
    public int Width { get; set; }
    public int Height { get; set; }
    public List<ComponentInfo> Components { get; set; }
}
