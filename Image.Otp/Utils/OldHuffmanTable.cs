namespace Image.Otp.Core.Utils;

public sealed class OldHuffmanTable
{
    public byte Class { get; init; }
    public byte Id { get; init; }
    public byte[] CodeLengths { get; init; } = [];
    public byte[] Symbols { get; init; } = [];
}