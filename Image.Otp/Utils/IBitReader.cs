namespace Image.Otp.Core.Utils;

public interface IBitReader
{
    int ReadBit();
    int ReadBits(int n, bool signed = true);
    int PeekBits(int n, bool signed = true);
    void ConsumeBits(int n);
}
