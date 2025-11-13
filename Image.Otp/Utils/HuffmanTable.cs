using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using static Image.Otp.Core.Utils.ISHuffmanTable;

namespace Image.Otp.Core.Utils;

[StructLayout(LayoutKind.Sequential)]
internal unsafe struct HuffmanTable
{
    /// <summary>
    /// Memory workspace buffer size used in <see cref="HuffmanTable"/> ctor.
    /// </summary>
    public const int WorkspaceByteSize = 256 * sizeof(uint);

    /// <summary>
    /// Derived from the DHT marker. Contains the symbols, in order of incremental code length.
    /// </summary>
    public fixed byte Values[256];

    /// <summary>
    /// Contains the largest code of length k (0 if none). MaxCode[17] is a sentinel to
    /// ensure <see cref="JpegBitReader.DecodeHuffman"/> terminates.
    /// </summary>
    public fixed ulong MaxCode[18];

    /// <summary>
    /// Values[] offset for codes of length k  ValOffset[k] = Values[] index of 1st symbol of code length
    /// k, less the smallest code of length k; so given a code of length k, the corresponding symbol is
    /// Values[code + ValOffset[k]].
    /// </summary>
    public fixed int ValOffset[19];

    /// <summary>
    /// Contains the length of bits for the given k value.
    /// </summary>
    public fixed byte LookaheadSize[Huffman.LookupSize];

    /// <summary>
    /// Lookahead table: indexed by the next <see cref="Huffman.LookupBits"/> bits of
    /// the input data stream.  If the next Huffman code is no more
    /// than <see cref="Huffman.LookupBits"/> bits long, we can obtain its length and
    /// the corresponding symbol directly from this tables.
    ///
    /// The lower 8 bits of each table entry contain the number of
    /// bits in the corresponding Huffman code, or <see cref="Huffman.LookupBits"/> + 1
    /// if too long.  The next 8 bits of each entry contain the symbol.
    /// </summary>
    public fixed byte LookaheadValue[Huffman.LookupSize];

    /// <summary>
    /// Initializes a new instance of the <see cref="HuffmanTable"/> struct.
    /// </summary>
    /// <param name="codeLengths">The code lengths.</param>
    /// <param name="values">The huffman values.</param>
    /// <param name="workspace">The provided spare workspace memory, can be dirty.</param>
    public HuffmanTable(ReadOnlySpan<byte> codeLengths, ReadOnlySpan<byte> values, Span<uint> workspace)
    {
        Unsafe.CopyBlockUnaligned(ref this.Values[0], ref MemoryMarshal.GetReference(values), (uint)values.Length);

        // Generate codes
        uint code = 0;
        int si = 1;
        int p = 0;
        for (int i = 1; i <= 16; i++)
        {
            int count = codeLengths[i];
            for (int j = 0; j < count; j++)
            {
                workspace[p++] = code;
                code++;
            }

            // 'code' is now 1 more than the last code used for codelength 'si'
            // in the valid worst possible case 'code' would have the least
            // significant bit set to 1, e.g. 1111(0) +1 => 1111(1)
            // but it must still fit in 'si' bits since no huffman code can be equal to all 1s
            // if last code is all ones, e.g. 1111(1), then incrementing it by 1 would yield
            // a new code which occupies one extra bit, e.g. 1111(1) +1 => (1)1111(0)
            if (code >= (1 << si))
            {
                throw new Exception("Bad huffman table.");
            }

            code <<= 1;
            si++;
        }

        // Figure F.15: generate decoding tables for bit-sequential decoding
        p = 0;
        for (int j = 1; j <= 16; j++)
        {
            if (codeLengths[j] != 0)
            {
                this.ValOffset[j] = p - (int)workspace[p];
                p += codeLengths[j];
                this.MaxCode[j] = workspace[p - 1]; // Maximum code of length l
                this.MaxCode[j] <<= Huffman.RegisterSize - j; // Left justify
                this.MaxCode[j] |= (1ul << (Huffman.RegisterSize - j)) - 1;
            }
            else
            {
                this.MaxCode[j] = 0;
            }
        }

        this.ValOffset[18] = 0;
        this.MaxCode[17] = ulong.MaxValue; // Ensures huff decode terminates

        // Compute lookahead tables to speed up decoding.
        // First we set all the table entries to Huffman.SlowBits, indicating "too long";
        // then we iterate through the Huffman codes that are short enough and
        // fill in all the entries that correspond to bit sequences starting
        // with that code.
        ref byte lookupSizeRef = ref this.LookaheadSize[0];
        Unsafe.InitBlockUnaligned(ref lookupSizeRef, Huffman.SlowBits, Huffman.LookupSize);

        p = 0;
        for (int length = 1; length <= Huffman.LookupBits; length++)
        {
            int jShift = Huffman.LookupBits - length;
            for (int i = 1; i <= codeLengths[length]; i++, p++)
            {
                // length = current code's length, p = its index in huffCode[] & Values[].
                // Generate left-justified code followed by all possible bit sequences
                int lookBits = (int)(workspace[p] << jShift);
                for (int ctr = 1 << (Huffman.LookupBits - length); ctr > 0; ctr--)
                {
                    this.LookaheadSize[lookBits] = (byte)length;
                    this.LookaheadValue[lookBits] = this.Values[p];
                    lookBits++;
                }
            }
        }
    }
}
