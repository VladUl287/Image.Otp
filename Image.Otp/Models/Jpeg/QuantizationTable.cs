namespace Image.Otp.Models.Jpeg;

public sealed class QuantizationTable
{
    public byte Id { get; init; }
    public ushort[] Values { get; init; } = [];
}
