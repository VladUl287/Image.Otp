namespace Image.Otp.Core.Constants;

public static class Huffman
{
    public const int MaxCodeLength = 16;

    public const int RegisterSize = 64;

    public const int MinBits = 16;

    public const int LookupBits = 8;

    public const int SlowBits = LookupBits + 1;

    public const int LookupSize = 1 << LookupBits;
}
