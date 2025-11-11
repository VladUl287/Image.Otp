using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Image.Otp.Core.Utils;

public unsafe sealed class ISHuffmanTable
{
    public const int WorkspaceByteSize = 256 * sizeof(uint);

    public byte[] Values = new byte[256];

    public ulong[] MaxCode = new ulong[18];

    public int[] ValOffset = new int[19];

    public byte[] LookaheadSize = new byte[Huffman.LookupSize];

    public byte[] LookaheadValue = new byte[Huffman.LookupSize];

    //public ISHuffmanTable(ReadOnlySpan<byte> codeLengths, ReadOnlySpan<byte> values, Span<uint> workspace)
    //{
    //    Unsafe.CopyBlockUnaligned(ref this.Values[0], ref MemoryMarshal.GetReference(values), (uint)values.Length);

    //    uint code = 0;
    //    int si = 1;
    //    int p = 0;
    //    for (int i = 0; i < 16; i++)
    //    {
    //        int count = codeLengths[i];
    //        for (int j = 0; j < count; j++)
    //        {
    //            workspace[p++] = code;
    //            code++;
    //        }

    //        if (code >= (1 << si))
    //        {
    //            throw new Exception("Bad huffman table.");
    //        }

    //        code <<= 1;
    //        si++;
    //    }

    //    // Figure F.15: generate decoding tables for bit-sequential decoding
    //    p = 0;
    //    for (int j = 0; j < 16; j++)
    //    {
    //        if (codeLengths[j] != 0)
    //        {
    //            this.ValOffset[j] = p - (int)workspace[p];
    //            p += codeLengths[j];
    //            this.MaxCode[j] = workspace[p - 1]; // Maximum code of length l
    //            this.MaxCode[j] <<= Huffman.RegisterSize - j; // Left justify
    //            this.MaxCode[j] |= (1ul << (Huffman.RegisterSize - j)) - 1;
    //        }
    //        else
    //        {
    //            this.MaxCode[j] = 0;
    //        }
    //    }

    //    this.ValOffset[18] = 0;
    //    this.MaxCode[17] = ulong.MaxValue; // Ensures huff decode terminates

    //    ref byte lookupSizeRef = ref this.LookaheadSize[0];
    //    Unsafe.InitBlockUnaligned(ref lookupSizeRef, Huffman.SlowBits, Huffman.LookupSize);

    //    p = 0;
    //    for (int length = 0; length <= Huffman.LookupBits; length++)
    //    {
    //        int jShift = Huffman.LookupBits - length;
    //        for (int i = 1; i <= codeLengths[length]; i++, p++)
    //        {
    //            // length = current code's length, p = its index in huffCode[] & Values[].
    //            // Generate left-justified code followed by all possible bit sequences
    //            int lookBits = (int)(workspace[p] << jShift);
    //            for (int ctr = 1 << (Huffman.LookupBits - length); ctr > 0; ctr--)
    //            {
    //                this.LookaheadSize[lookBits] = (byte)length;
    //                this.LookaheadValue[lookBits] = this.Values[p];
    //                lookBits++;
    //            }
    //        }
    //    }
    //}

    public ISHuffmanTable(ReadOnlySpan<byte> codeLengths, ReadOnlySpan<byte> values, Span<uint> workspace)
    {
        // Step 1: Copy the symbol values
        values.CopyTo(Values);

        // Step 2: Generate Huffman codes for each length
        uint currentCode = 0;
        int symbolIndex = 1;
        int workspacePosition = 0;

        for (int codeLength = 1; codeLength <= 16; codeLength++)
        {
            int symbolsWithThisLength = codeLengths[codeLength - 1];

            // Assign consecutive codes to symbols of this length
            for (int j = 0; j < symbolsWithThisLength; j++)
            {
                workspace[workspacePosition++] = currentCode;
                currentCode++;
            }

            // Validate the code doesn't exceed expected size
            if (currentCode >= (1 << symbolIndex))
            {
                throw new Exception("Invalid Huffman table: codes overflow");
            }

            // Prepare for next code length (shift left by 1 bit)
            currentCode <<= 1;
            symbolIndex++;
        }

        // Step 3: Build decoding tables
        workspacePosition = 0;
        for (int codeLength = 1; codeLength <= 16; codeLength++)
        {
            int symbolsWithThisLength = codeLengths[codeLength - 1];

            if (symbolsWithThisLength > 0)
            {
                // Calculate value offset for this code length
                this.ValOffset[codeLength - 1] = workspacePosition - (int)workspace[workspacePosition];

                // Move to next position
                workspacePosition += symbolsWithThisLength;

                // Get the maximum code for this length and left-justify it
                uint maxCode = workspace[workspacePosition - 1];
                this.MaxCode[codeLength - 1] = LeftJustifyCode(maxCode, codeLength);
            }
            else
            {
                this.MaxCode[codeLength - 1] = 0;
            }
        }

        // Step 4: Set termination values
        this.ValOffset[18] = 0;
        this.MaxCode[17] = ulong.MaxValue; // Ensures decoding terminates

        // Step 5: Initialize lookup table for fast decoding
        InitializeLookupTable(codeLengths, workspace);
    }

    private static ulong LeftJustifyCode(uint code, int codeLength)
    {
        int shiftAmount = Huffman.RegisterSize - codeLength;
        ulong leftJustified = (ulong)code << shiftAmount;

        // Set all lower bits to 1 for comparison during decoding
        return leftJustified | ((1ul << shiftAmount) - 1);
    }

    private void InitializeLookupTable(ReadOnlySpan<byte> codeLengths, Span<uint> workspace)
    {
        // Initialize all entries to use slow decoding
        Array.Fill<byte>(LookaheadSize, Huffman.SlowBits);

        int workspacePosition = 0;

        for (int codeLength = 1; codeLength <= Huffman.LookupBits; codeLength++)
        {
            int symbolsWithThisLength = codeLengths[codeLength - 1];
            int shiftAmount = Huffman.LookupBits - codeLength;

            for (int i = 0; i < symbolsWithThisLength; i++, workspacePosition++)
            {
                uint code = workspace[workspacePosition];
                byte value = this.Values[workspacePosition];

                // Generate all possible lookahead combinations for this code
                int baseLookupIndex = (int)(code << shiftAmount);
                int combinations = 1 << (Huffman.LookupBits - codeLength);

                for (int combination = 0; combination < combinations; combination++)
                {
                    int lookupIndex = baseLookupIndex + combination;
                    this.LookaheadSize[lookupIndex] = (byte)codeLength;
                    this.LookaheadValue[lookupIndex] = value;
                }
            }
        }
    }

    public static class Huffman
    {
        /// <summary>
        /// The size of the huffman decoder register.
        /// </summary>
        public const int RegisterSize = 64;

        /// <summary>
        /// The number of bits to fetch when filling the <see cref="JpegBitReader"/> buffer.
        /// </summary>
        public const int FetchBits = 48;

        /// <summary>
        /// The number of times to read the input stream when filling the <see cref="JpegBitReader"/> buffer.
        /// </summary>
        public const int FetchLoop = FetchBits / 8;

        /// <summary>
        /// The minimum number of bits allowed before by the <see cref="JpegBitReader"/> before fetching.
        /// </summary>
        public const int MinBits = RegisterSize - FetchBits;

        /// <summary>
        /// If the next Huffman code is no more than this number of bits, we can obtain its length
        /// and the corresponding symbol directly from this tables.
        /// </summary>
        public const int LookupBits = 8;

        /// <summary>
        /// If a Huffman code is this number of bits we cannot use the lookup table to determine its value.
        /// </summary>
        public const int SlowBits = LookupBits + 1;

        /// <summary>
        /// The size of the lookup table.
        /// </summary>
        public const int LookupSize = 1 << LookupBits;
    }
}
