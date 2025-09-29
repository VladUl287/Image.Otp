namespace Image.Otp.Core.Models.Jpeg;

public sealed class HuffmanTable
{
    public byte Class { get; init; }
    public byte Id { get; init; }
    public byte[] CodeLengths { get; init; } = [];
    public byte[] Symbols { get; init; } = [];
}
