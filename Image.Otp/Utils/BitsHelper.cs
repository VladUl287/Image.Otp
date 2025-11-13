namespace Image.Otp.Core.Utils;

public static class BitsHelper
{
    public static int ToNumber(this Span<int> bits, bool signed)
    {
        if (signed)
        {
            if (bits[0] == 1)
            {
                return BitToNumber(bits);
            }
            else
            {
                for (int i = 0; i < bits.Length; i++)
                    bits[i] = 1 - bits[i];

                return -BitToNumber(bits);
            }
        }
        return BitToNumber(bits);
    }

    private static int BitToNumber(Span<int> bits)
    {
        var res = 0;
        for (int i = 0; i < bits.Length; i++)
        {
            res = (res << 1) | bits[i];
        }
        return res;
    }
}
