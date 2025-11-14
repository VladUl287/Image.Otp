using Image.Otp.Core.Constants;

namespace Image.Otp.Core.Utils;

public interface IBitReader
{
    int ReadBit();
    bool EnsureBits(int minBits = Huffman.MinBits);
    int ReadBits(int n, bool signed = true);
    int PeekBits(int n, bool signed = true);
    void ConsumeBits(int n);
}
