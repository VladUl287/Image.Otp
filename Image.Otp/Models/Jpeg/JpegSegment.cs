namespace Image.Otp.Models.Jpeg;

public sealed class JpegSegment
{
    public int Offset { get; init; }
    public byte Marker { get; init; }
    public string Name { get; init; }
    public int Length { get; init; }

    public byte[] Data { get; init; } = [];

    public override string ToString()
    {
        return $"{Offset:X6}: {Name} (0xFF){Marker:X2} {(Length>0 ? $"len={Length}" : "no-len")} payload={Data?.Length ?? 0}";
    }
}
