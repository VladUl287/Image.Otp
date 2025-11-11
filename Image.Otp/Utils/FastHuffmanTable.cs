using System.Runtime.CompilerServices;

namespace Image.Otp.Core.Utils;

public sealed class FastHuffmanTable : IHuffmanTable
{
    private readonly ushort[] _lookup; // 64K direct lookup table
    private readonly byte[] _sizeTable; // Code sizes for validation

    public FastHuffmanTable(ReadOnlySpan<byte> bits, ReadOnlySpan<byte> values)
    {
        _lookup = new ushort[65536]; // 64K entries for 16-bit lookups
        _sizeTable = new byte[256]; // Store code sizes

        BuildAcceleratedTable(bits, values);
    }

    private void BuildAcceleratedTable(ReadOnlySpan<byte> bits, ReadOnlySpan<byte> values)
    {
        ushort code = 0;
        int valueIndex = 0;

        // First pass: build size table and initial codes
        for (int codeLength = 1; codeLength <= 16; codeLength++)
        {
            int numCodes = bits[codeLength - 1];

            for (int i = 0; i < numCodes; i++)
            {
                byte value = values[valueIndex++];
                _sizeTable[value] = (byte)codeLength;
                code++;
            }
            code <<= 1;
        }

        // Second pass: build 16-bit wide lookup table
        code = 0;
        valueIndex = 0;

        for (int codeLength = 1; codeLength <= 16; codeLength++)
        {
            int numCodes = bits[codeLength - 1];
            int shift = 16 - codeLength;

            for (int i = 0; i < numCodes; i++)
            {
                byte value = values[valueIndex++];
                ushort baseCode = (ushort)(code << shift);

                // Fill all possible extensions of this code
                int fillCount = 1 << shift;
                for (int j = 0; j < fillCount; j++)
                {
                    _lookup[baseCode | j] = (ushort)((value << 8) | codeLength);
                }

                code++;
            }
            code <<= 1;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public (byte symbol, byte length) Decode(uint bits16)
    {
        ushort result = _lookup[bits16 & 0xFFFF];
        return ((byte)(result >> 8), (byte)(result & 0xFF));
    }
}
