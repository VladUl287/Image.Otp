using System.Runtime.InteropServices;

namespace Image.Otp.Models.Jpeg;

public sealed class QuantizationTable
{
    public byte Id { get; init; }
    public ushort[] Values { get; init; } = [];
}

[StructLayout(LayoutKind.Sequential)]
public unsafe struct QuantizationTableBuffer
{
    public fixed ushort Table[64];
}
