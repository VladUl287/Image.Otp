namespace Image.Otp.Core.Utils;

public interface IBitReader
{
    void ConsumeBits(int n);
    int PeekBits(int n, bool signed = true);
    int ReadBits(int n, bool signed = true);
}