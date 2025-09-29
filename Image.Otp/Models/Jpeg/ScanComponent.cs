namespace Image.Otp.Core.Models.Jpeg;

public sealed class ScanComponent
{
    public byte ComponentId { get; init; }
    public byte DcHuffmanTableId { get; init; }
    public byte AcHuffmanTableId { get; init; }
}

public sealed class ScanInfo
{
    public List<ScanComponent> Components { get; init; } = [];
    public byte Ss { get; init; }  // Start of spectral selection (0 for baseline)
    public byte Se { get; init; }  // End of spectral selection (63 for baseline)
    public byte Ah { get; init; }  // Approximation high (baseline: 0)
    public byte Al { get; init; }  // Approximation low (baseline: 0)
}


public sealed class SOSSegment
{
    public ScanComponent[] Components { get; init; } = [];
    public byte Ss { get; init; }  // Start of spectral selection (0 for baseline)
    public byte Se { get; init; }  // End of spectral selection (63 for baseline)
    public byte Ah { get; init; }  // Approximation high (baseline: 0)
    public byte Al { get; init; }  // Approximation low (baseline: 0)
}
