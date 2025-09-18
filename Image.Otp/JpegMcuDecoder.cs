using Image.Otp.Helpers;
using Image.Otp.Models.Jpeg;

namespace Image.Otp;

public static class JpegMcuDecoder
{
    // Decode a single Huffman-coded symbol using canonical table.
    // Returns -1 on marker/EOF.
    //private static int DecodeHuffmanSymbol(CustomBitReader br, CanonicalHuffmanTable table)
    //{
    //    int code = 0;
    //    for (int length = 1; length <= 16; length++)
    //    {
    //        int bit = br.ReadBit();
    //        if (bit < 0) return -1;
    //        code = (code << 1) | bit;
    //        if (table.TryGetSymbol(code, length, out byte sym))
    //        {
    //            //Console.WriteLine($"id = {table.Id} len={length} sym = {sym} code={Convert.ToString(code, 2).PadLeft(length, '0')}");

    //            return sym;
    //        }
    //    }
    //    return -1;
    //    Console.WriteLine($"id={table.Id} code={Convert.ToString(code, 2).PadLeft(16, '0')}");
    //    throw new InvalidDataException("Invalid Huffman code (no symbol within 16 bits).");
    //}

    //public static int DecodeHuffmanSymbol(CustomBitReader bitReader, CanonicalHuffmanTable table)
    //{
    //    int code = 0;
    //    int length = 0;

    //    // Read bits until we find a matching Huffman code
    //    while (length < 32) // Limit to prevent infinite loop
    //    {
    //        code = (code << 1) | bitReader.ReadBit();
    //        length++;

    //        if (table.TryGetSymbol(code, length, out var symbol))
    //        {
    //            return symbol;
    //        }
    //    }

    //    throw new Exception("Invalid Huffman code");
    //}

    private static int DecodeHuffmanSymbol(CustomBitReader br, CanonicalHuffmanTable table)
    {
        int code = 0;
        for (int length = 1; length <= 16; length++)
        {
            int bit = br.ReadBit();
            if (bit < 0)
            {
                //Console.WriteLine($"[DecodeHuffmanSymbol] Marker/EOF encountered at bitLength={length}, code=0b{Convert.ToString(code, 2)}");
                return -1;
            }

            code = (code << 1) | bit;

            if (table.TryGetSymbol(code, length, out byte sym))
            {
                return sym;
            }
        }

        throw new InvalidDataException("Invalid Huffman code (no symbol within 16 bits).");
    }

    // Find next marker in stream from current byte-aligned position.
    private static int FindNextMarker(CustomBitReader br)
    {
        // position is already byte-aligned outside; scan bytes until 0xFF then marker byte.
        while (true)
        {
            int b = br.ReadRawByte();
            if (b < 0) return -1;
            if (b == 0xFF)
            {
                // skip any 0xFF fills
                int second;
                do
                {
                    second = br.ReadRawByte();
                    if (second < 0) return -1;
                } while (second == 0xFF);

                if (second == 0x00)
                {
                    // stuffed 0xFF data byte, continue scanning
                    continue;
                }
                return second;
            }
        }
    }

    private static readonly Dictionary<byte, int> dcPredictor = new();
    public static List<MCUBlock> DecodeScanToBlocksProgressive(
        byte[] compressed,
        FrameInfo frame,
        ScanInfo scan,
        Dictionary<byte, CanonicalHuffmanTable> huffDc,
        Dictionary<byte, CanonicalHuffmanTable> huffAc,
        int restartInterval,
        List<MCUBlock>? previousMcus = null) // Add parameter for previous state in progressive
    {
        if (frame == null) throw new ArgumentNullException(nameof(frame));
        if (scan == null) throw new ArgumentNullException(nameof(scan));
        if (compressed == null) throw new ArgumentNullException(nameof(compressed));

        var compMap = frame.Components.ToDictionary(c => c.Id);
        var scanComponents = scan.Components;

        int maxH = frame.Components.Max(c => c.HorizontalSampling);
        int maxV = frame.Components.Max(c => c.VerticalSampling);

        int mcuCols = (frame.Width + (8 * maxH - 1)) / (8 * maxH);
        int mcuRows = (frame.Height + (8 * maxV - 1)) / (8 * maxV);

        var bitReader = new CustomBitReader(compressed);

        var result = previousMcus ?? [];
        if (previousMcus is null)
        {
            InitilizeEmptyMcus(frame, mcuCols, mcuRows, result);
        }

        foreach (var sc in scanComponents)
        {
            if (!dcPredictor.ContainsKey(sc.ComponentId))
                dcPredictor[sc.ComponentId] = 0;
        }

        int restartCounter = restartInterval;

        bool isDcScan = (scan.Ss == 0 && scan.Se == 0);
        bool isAcScan = !isDcScan;
        bool isRefinement = (scan.Ah > 0);

        int length_EOB_run = 0;
        for (int my = 0; my < mcuRows; my++)
        {
            for (int mx = 0; mx < mcuCols; mx++)
            {
                int mcuIndex = my * mcuCols + mx;
                var mcu = result[mcuIndex];

                foreach (var sc in scanComponents)
                {
                    var comp = compMap[sc.ComponentId];
                    int h = comp.HorizontalSampling;
                    int v = comp.VerticalSampling;

                    int blocksPerMcu = h * v;
                    var blocks = mcu.ComponentBlocks[sc.ComponentId];

                    var dcTable = huffDc[sc.DcHuffmanTableId];

                    for (int b = 0; b < blocksPerMcu; b++)
                    {
                        short[] zz = blocks[b];

                        if (isDcScan)
                        {
                            if (isRefinement)
                                ProcessDcRefinementScan(scan, bitReader, zz);
                            else
                            {
                                ProcessDcFirstScan(bitReader, dcPredictor, sc, dcTable, zz);
                                zz[0] <<= scan.Al;
                            }
                            blocks[b] = zz;
                        }
                        else // AC scan
                        {
                            var acTable = huffAc[sc.AcHuffmanTableId];
                            if (isRefinement)
                            {
                                if (mx == 5 && my == 1 && zz[0] == 1016)
                                {

                                }

                                if (mx == 21 && my == 0 && zz[0] == -152)
                                {

                                }

                                //ProcessAcRefinementScan(scan, bitReader, zz, acTable);
                                length_EOB_run = DecodeACsProgressiveSubsequentPerBlock(scan, bitReader, zz, acTable, length_EOB_run);
                            }
                            else
                            {
                                if (mx == 21 && my == 0 && zz[0] == -152)
                                {

                                }

                                //ProcessAcFirstScan(scan, bitReader, zz, acTable);
                                length_EOB_run = DecodeACsProgressiveFirstPerBlock(scan, bitReader, zz, acTable, length_EOB_run);
                            }

                            blocks[b] = zz;
                        }

                    }
                    mcu.ComponentBlocks[sc.ComponentId] = blocks;
                }

                result[mcuIndex] = mcu;
            }
        }

        return result;
    }

    private static void InitilizeEmptyMcus(FrameInfo frame, int mcuCols, int mcuRows, List<MCUBlock> result)
    {
        for (int my = 0; my < mcuRows; my++)
        {
            for (int mx = 0; mx < mcuCols; mx++)
            {
                var mcu = new MCUBlock { X = mx, Y = my };
                foreach (var comp in frame.Components)
                {
                    int blocksPerMcu = comp.HorizontalSampling * comp.VerticalSampling;
                    mcu.ComponentBlocks[comp.Id] = new List<short[]>();
                    for (int i = 0; i < blocksPerMcu; i++)
                    {
                        mcu.ComponentBlocks[comp.Id].Add(new short[64]); // Initialize with zeros
                    }
                }
                result.Add(mcu);
            }
        }
    }

    private static void ProcessDcRefinementScan(ScanInfo scan, CustomBitReader bitReader, short[] zz)
    {
        int bit = bitReader.ReadBit();
        if (bit == 1)
        {
            zz[0] |= (short)(1 << scan.Al);
        }
    }

    private static void ProcessDcFirstScan(CustomBitReader bitReader, Dictionary<byte, int> dcPredictor, ScanComponent sc, CanonicalHuffmanTable dcTable, short[] zz)
    {
        int sym = DecodeHuffmanSymbol(bitReader, dcTable);
        if (sym < 0)
        {
            throw new EndOfStreamException("Marker or EOF encountered while decoding DC.");
        }
        int magnitude = sym;
        int dcDiff = 0;
        if (magnitude > 0)
        {
            int bits = bitReader.ReadBits(magnitude, false);
            if (bits < 0) throw new EndOfStreamException("EOF/marker while reading DC bits.");
            dcDiff = JpegDecoderHelpers.ExtendSign(bits, magnitude);
        }

        int prevDc = dcPredictor[sc.ComponentId];
        int dcVal = prevDc + dcDiff;

        dcPredictor[sc.ComponentId] = dcVal;
        zz[0] = (short)dcVal;
    }

    private static int DecodeACsProgressiveSubsequentPerBlock(ScanInfo scan, CustomBitReader bitReader, short[] zz, CanonicalHuffmanTable acTable, int lengthEOBRun)
    {
        int Ss = scan.Ss, Se = scan.Se, Al = scan.Al;
        int idx = Ss;

        // this is a EOB
        if (lengthEOBRun > 0)
        {
            while (idx <= Se)
            {
                if (zz[idx] != 0)
                {
                    RefineAC(bitReader, zz, idx, Al);
                }
                idx++;
            }
            return lengthEOBRun - 1;
        }

        while (idx <= Se)
        {
            int symbol = DecodeHuffmanSymbol(bitReader, acTable);
            int RUNLENGTH = symbol >> 4;
            int SIZE = symbol & 0x0F;

            if (SIZE == 1) // zero history
            {
                int val = bitReader.ReadBits(SIZE) << Al;
                while (RUNLENGTH > 0 || zz[idx] != 0)
                {
                    if (zz[idx] != 0)
                    {
                        RefineAC(bitReader, zz, idx, Al);
                    }
                    else
                    {
                        RUNLENGTH--;
                    }
                    idx++;
                }
                zz[idx] = (short)val;
                idx++;
            }
            else if (SIZE == 0)
            {
                if (RUNLENGTH < 15) // EOBn, n=0-14 
                {
                    // !!! read EOB run first
                    int newEOBrun = bitReader.ReadBits(RUNLENGTH, false) + (1 << RUNLENGTH);
                    while (idx <= Se)
                    {
                        if (zz[idx] != 0)
                        {
                            RefineAC(bitReader, zz, idx, Al);
                        }
                        idx++;
                    }
                    return newEOBrun - 1;
                }
                else // ZRL(15,0)
                {
                    while (RUNLENGTH >= 0)
                    {
                        if (zz[idx] != 0)
                        {
                            RefineAC(bitReader, zz, idx, Al);
                        }
                        else
                        {
                            RUNLENGTH--;
                        }
                        idx++;
                    }
                }
            }
        }
        return 0;
    }

    private static void RefineAC(CustomBitReader stream, short[] block, int idx, int Al)
    {
        short val = block[idx];
        if (val > 0)
        {
            if (stream.ReadBit() == 1)
            {
                block[idx] += (short)(1 << Al);
            }
        }
        else if (val < 0)
        {
            if (stream.ReadBit() == 1)
            {
                block[idx] += (short)((-1) << Al);
            }
        }
    }

    private static int DecodeACsProgressiveFirstPerBlock(ScanInfo scan, CustomBitReader bitReader, short[] zz, CanonicalHuffmanTable acTable, int lengthEOBRun)
    {
        int Ss = scan.Ss, Se = scan.Se, Al = scan.Al;

        // this is a EOB
        if (lengthEOBRun > 0)
        {
            return lengthEOBRun - 1;
        }

        int idx = Ss;
        while (idx <= Se)
        {
            int symbol = DecodeHuffmanSymbol(bitReader, acTable);
            int RUNLENGTH = symbol >> 4;
            int SIZE = symbol & 0x0F; // Equivalent to % (2**4)

            if (SIZE == 0)
            {
                if (RUNLENGTH == 15) // ZRL(15,0)
                {
                    idx += 16;
                }
                else // EOBn, n=0-14
                {
                    return bitReader.ReadBits(RUNLENGTH, false) + (1 << RUNLENGTH) - 1;
                }
            }
            else
            {
                idx += RUNLENGTH;
                zz[idx] = (short)(bitReader.ReadBits(SIZE) << Al);
                idx += 1;
            }
        }
        return 0;
    }

    public static List<MCUBlock> DecodeScanToBlocks(
        byte[] compressed,
        FrameInfo frame,
        ScanInfo scan,
        Dictionary<byte, QuantizationTable> qTables,
        Dictionary<byte, CanonicalHuffmanTable> huffDc,
        Dictionary<byte, CanonicalHuffmanTable> huffAc,
        int restartInterval)
    {
        if (frame == null) throw new ArgumentNullException(nameof(frame));
        if (scan == null) throw new ArgumentNullException(nameof(scan));
        if (compressed == null) throw new ArgumentNullException(nameof(compressed));

        // Preconditions for baseline non-progressive
        if (scan.Ss != 0 || scan.Se != 63 || scan.Ah != 0 || scan.Al != 0)
            throw new ArgumentException("This decoder only supports baseline non-progressive scans (Ss=0,Se=63,Ah=0,Al=0).");

        // Build map componentId -> component info
        var compMap = frame.Components.ToDictionary(c => c.Id);

        // Build scan component mapping (list in scan order)
        var scanComponents = scan.Components;

        // MCU sampling grid sizes
        int maxH = frame.Components.Max(c => c.HorizontalSampling);
        int maxV = frame.Components.Max(c => c.VerticalSampling);

        // Number of MCUs horizontally and vertically
        int mcuCols = (frame.Width + (8 * maxH - 1)) / (8 * maxH);
        int mcuRows = (frame.Height + (8 * maxV - 1)) / (8 * maxV);

        var bitReader = new CustomBitReader(compressed);

        var result = new List<MCUBlock>();

        // DC predictors per component id
        var dcPredictor = new Dictionary<byte, int>();
        foreach (var sc in scanComponents) dcPredictor[sc.ComponentId] = 0;

        int restartCounter = restartInterval;
        int expectedRst = 0; // 0..7 for RST0..RST7

        // Iterate MCUs row-major
        for (int my = 0; my < mcuRows; my++)
        {
            for (int mx = 0; mx < mcuCols; mx++)
            {
                // Check restart handling before MCU when restartInterval > 0
                if (restartInterval > 0 && restartCounter == 0 && (mx != 0 || my != 0))
                {
                    // Align and find marker
                    bitReader.AlignToByte();
                    int marker = FindNextMarker(bitReader);
                    if (marker < 0) throw new EndOfStreamException("Unexpected EOF while searching for restart marker.");
                    if (marker < 0xD0 || marker > 0xD7)
                    {
                        // Not a restart marker; allow other markers? For baseline, usually we expect RSTn.
                        throw new InvalidDataException($"Expected restart marker RSTn but found 0xFF{marker:X2}.");
                    }
                    int rstNum = marker - 0xD0;
                    if (rstNum != expectedRst)
                    {
                        // Not matching expected, but we can still resync: reset expected to this.
                        expectedRst = rstNum;
                    }

                    // Reset DC predictors
                    var keys = new List<byte>(dcPredictor.Keys);
                    foreach (var k in keys) dcPredictor[k] = 0;

                    // Consume marker and continue
                    expectedRst = (expectedRst + 1) & 7;
                    restartCounter = restartInterval;
                }

                var mcu = new MCUBlock { X = mx, Y = my };

                // For each component in the scan (in scan order)
                foreach (var sc in scanComponents)
                {
                    var comp = compMap[sc.ComponentId];
                    int h = comp.HorizontalSampling;
                    int v = comp.VerticalSampling;

                    // number of blocks for this component in MCU
                    int blocksPerMcu = h * v;
                    if (!mcu.ComponentBlocks.TryGetValue(sc.ComponentId, out var list))
                    {
                        list = new List<short[]>();
                        mcu.ComponentBlocks[sc.ComponentId] = list;
                    }

                    var qTable = qTables[comp.QuantizationTableId];
                    if (qTable == null) throw new InvalidOperationException($"Missing quantization table {comp.QuantizationTableId}");

                    var dcTable = huffDc[sc.DcHuffmanTableId];
                    var acTable = huffAc[sc.AcHuffmanTableId];

                    for (int b = 0; b < blocksPerMcu; b++)
                    {
                        // Decode one 8x8 block (baseline single scan)
                        short[] zz = new short[64]; // zig-zag ordered coefficients
                        // --- DC ---
                        int sym = DecodeHuffmanSymbol(bitReader, dcTable);
                        if (sym < 0)
                        {
                            throw new EndOfStreamException("Marker or EOF encountered while decoding DC.");
                        }
                        int magnitude = sym; // number of additional bits
                        int dcDiff = 0;
                        if (magnitude > 0)
                        {
                            int bits = bitReader.ReadBits(magnitude, false);
                            if (bits < 0) throw new EndOfStreamException("EOF/marker while reading DC bits.");
                            dcDiff = JpegDecoderHelpers.ExtendSign(bits, magnitude);
                        }
                        int prevDc = dcPredictor[sc.ComponentId];
                        int dcVal = prevDc + dcDiff;
                        dcPredictor[sc.ComponentId] = dcVal;
                        zz[0] = (short)dcVal;

                        // --- AC ---
                        int k = 1;
                        while (k < 64)
                        {
                            int acSym = DecodeHuffmanSymbol(bitReader, acTable);
                            if (acSym < 0) throw new EndOfStreamException("Marker or EOF encountered while decoding AC.");
                            if (acSym == 0x00)
                            {
                                // EOB - rest are zero
                                break;
                            }
                            if (acSym == 0xF0)
                            {
                                // ZRL - run of 16 zeros
                                k += 16;
                                continue;
                            }
                            int run = (acSym >> 4) & 0x0F;
                            int size = acSym & 0x0F;
                            k += run;
                            if (k >= 64)
                                throw new InvalidDataException("Run exceeds block size while decoding AC.");
                            int bits = 0;
                            if (size > 0)
                            {
                                bits = bitReader.ReadBits(size, false);
                                if (bits < 0) throw new EndOfStreamException("EOF/marker while reading AC bits.");
                            }
                            int level = JpegDecoderHelpers.ExtendSign(bits, size);
                            zz[k] = (short)level;
                            k++;
                        }

                        // Dequantize (multiply by table values) and convert zigzag to row-major
                        short[] dequantZz = new short[64];
                        for (int i = 0; i < 64; i++)
                        {
                            //dequantZz[i] = (short)(zz[i] * qTable.Values[i]);
                            dequantZz[i] = zz[i];
                        }
                        short[] block = JpegDecoderHelpers.NaturalToZigzag(dequantZz);
                        list.Add(block);
                    }
                }

                result.Add(mcu);

                if (restartInterval > 0)
                {
                    restartCounter--;
                    if (restartCounter < 0) restartCounter = 0;
                }
            }
        }

        return result;
    }
}

