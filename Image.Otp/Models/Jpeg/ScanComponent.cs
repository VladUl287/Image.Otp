namespace Image.Otp.Models.Jpeg;

public class ScanComponent
{
    public byte ComponentId { get; set; }
    public byte DcHuffmanTableId { get; set; }
    public byte AcHuffmanTableId { get; set; }
}

public class ScanInfo
{
    public List<ScanComponent> Components { get; set; }
    public byte Ss { get; set; }  // Start of spectral selection (0 for baseline)
    public byte Se { get; set; }  // End of spectral selection (63 for baseline)
    public byte Ah { get; set; }  // Approximation high (baseline: 0)
    public byte Al { get; set; }  // Approximation low (baseline: 0)
}
