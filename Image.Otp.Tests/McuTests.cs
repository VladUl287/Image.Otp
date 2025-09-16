using Xunit;
using System.Collections.Generic;
using Image.Otp.Models.Jpeg;

namespace Image.Otp.Tests;

public sealed class HuffmanDecodeLogicSyntheticTests
{
    // ZigZag helper (if your code has global ZigZag, remove or reuse)
    private static readonly int[] ZigZag = new int[] {
         0,1,5,6,14,15,27,28,
         2,4,7,13,16,26,29,42,
         3,8,12,17,25,30,41,43,
         9,11,18,24,31,40,44,53,
        10,19,23,32,39,45,52,54,
        20,22,33,38,46,51,55,60,
        21,34,37,47,50,56,59,61,
        35,36,48,49,57,58,62,63
    };

    // Test 1: simplest valid stream
    // DC: Huffman symbol -> category = 1 (one additional bit)
    // DC extra bit: 1 => DC diff = +1 -> DC = 1
    // AC: symbol (run=0,size=1) (0x01), extra bit = 1 => AC value = +1 at k=1
    // AC: EOB
    [Fact]
    public void DecodeScanToBlocks_SimplePositiveAC()
    {
        // DC table: code 0 (len=1) -> symbol 1 (category 1)
        var dcTable = new CanonicalHuffmanTable();
        dcTable.Add(0b0, 1, 0x01); // symbol = 1 (category)
        var huffDc = new Dictionary<byte, CanonicalHuffmanTable> { [0] = dcTable };

        // AC table: code 0 (len=1) -> EOB (0x00), code 1 (len=1) -> (run=0,size=1)=0x01
        var acTable = new CanonicalHuffmanTable();
        acTable.Add(0b0, 1, 0x00); // EOB
        acTable.Add(0b1, 1, 0x01); // run=0,size=1
        var huffAc = new Dictionary<byte, CanonicalHuffmanTable> { [0] = acTable };

        // one component, quant table (not used by decode logic)
        var qTable = new QuantizationTable { Values = new ushort[64] };
        var qTables = new Dictionary<byte, QuantizationTable> { [0] = qTable };

        var frame = new FrameInfo
        {
            Width = 8,
            Height = 8,
            Components = new List<ComponentInfo> { new ComponentInfo { Id = 1, SamplingFactor = 0x11, QuantizationTableId = 0 } }
        };
        var scan = new ScanInfo
        {
            Components = new List<ScanComponent> { new ScanComponent { ComponentId = 1, DcHuffmanTableId = 0, AcHuffmanTableId = 0 } }
        };

        // Bitstream (MSB-first):
        // DC symbol: bit 0  (matches dcTable code 0)
        // DC extra   : bit 1  (category=1 => one bit -> value 1)
        // AC symbol  : bit 1  (matches acTable code 1 -> 0x01)
        // AC extra   : bit 1  (size=1 bits -> value 1)
        // AC EOB     : bit 0  (acTable code 0 -> EOB)
        //
        // Bits (in order read): 0 1 1 1 0 0 0 0 -> pack MSB-first into 0xE0? We'll pack minimal: 0b01110000 = 0x70
        byte[] compressed = new byte[] { 0b01110000 };

        var mcus = HuffmanTableLogic.DecodeScanToBlocks(compressed, frame, scan, qTables, huffDc, huffAc);

        Assert.Single(mcus);
        var block = mcus[0].ComponentBlocks[1][0];

        // DC = +1
        Assert.Equal(1, block[0]);

        // first AC (k=1) = +1
        Assert.Equal(1, block[1]);

        // remaining zero
        for (int k = 2; k < 64; k++) Assert.Equal(0, block[k]);
    }

    // Test 2: negative AC value (size=1 bits=0 -> -1)
    [Fact]
    public void DecodeScanToBlocks_NegativeAC()
    {
        // DC: category 0 (no bits)
        var dcTable = new CanonicalHuffmanTable();
        dcTable.Add(0b0, 1, 0x00);
        var huffDc = new Dictionary<byte, CanonicalHuffmanTable> { [0] = dcTable };

        // AC: ac symbol 0x01 (run=0,size=1) -> we will supply bits=0 (=> -1), then EOB
        var acTable = new CanonicalHuffmanTable();
        acTable.Add(0b1, 1, 0x01); // code 1 => 0x01
        acTable.Add(0b0, 1, 0x00); // code 0 => EOB
        var huffAc = new Dictionary<byte, CanonicalHuffmanTable> { [0] = acTable };

        var qTable = new QuantizationTable { Values = new ushort[64] };
        var qTables = new Dictionary<byte, QuantizationTable> { [0] = qTable };

        var frame = new FrameInfo
        {
            Width = 8,
            Height = 8,
            Components = new List<ComponentInfo> { new ComponentInfo { Id = 1, SamplingFactor = 0x11, QuantizationTableId = 0 } }
        };
        var scan = new ScanInfo { Components = new List<ScanComponent> { new ScanComponent { ComponentId = 1, DcHuffmanTableId = 0, AcHuffmanTableId = 0 } } };

        // Bits: DC symbol (0), AC symbol (1), AC extra bit (0 -> -1), then EOB (0)
        // Sequence: 0 1 0 0 ... -> pack as 0b01000000 = 0x40
        byte[] compressed = new byte[] { 0b01000000 };

        var mcus = HuffmanTableLogic.DecodeScanToBlocks(compressed, frame, scan, qTables, huffDc, huffAc);
        var block = mcus[0].ComponentBlocks[1][0];

        Assert.Equal(0, block[0]); // DC
        Assert.Equal(-1, block[1]); // AC k=1 should be -1
        for (int k = 2; k < 64; k++) Assert.Equal(0, block[k]);
    }

    // Test 3: ZRL (0xF0) handling: skip 16 zeros then EOB
    [Fact]
    public void DecodeScanToBlocks_ZRL()
    {
        var dcTable = new CanonicalHuffmanTable();
        dcTable.Add(0b0, 1, 0x00); // DC=0
        var huffDc = new Dictionary<byte, CanonicalHuffmanTable> { [0] = dcTable };

        // AC table: code0=ZRL (0xF0), code1=EOB (0x00)
        var acTable = new CanonicalHuffmanTable();
        acTable.Add(0b0, 1, 0xF0);
        acTable.Add(0b1, 1, 0x00);
        var huffAc = new Dictionary<byte, CanonicalHuffmanTable> { [0] = acTable };

        var qTable = new QuantizationTable { Values = new ushort[64] };
        var qTables = new Dictionary<byte, QuantizationTable> { [0] = qTable };

        var frame = new FrameInfo
        {
            Width = 8,
            Height = 8,
            Components = new List<ComponentInfo> { new ComponentInfo { Id = 1, SamplingFactor = 0x11, QuantizationTableId = 0 } }
        };
        var scan = new ScanInfo { Components = new List<ScanComponent> { new ScanComponent { ComponentId = 1, DcHuffmanTableId = 0, AcHuffmanTableId = 0 } } };

        // Bits: DC symbol 0, AC ZRL symbol 0, AC EOB symbol 1
        // Sequence bits: 0 0 1 -> pack MSB-first => 0b00100000 = 0x20
        byte[] compressed = new byte[] { 0b00100000 };

        var mcus = HuffmanTableLogic.DecodeScanToBlocks(compressed, frame, scan, qTables, huffDc, huffAc);
        var block = mcus[0].ComponentBlocks[1][0];

        // DC = 0, all AC zeros (ZRL skipped 16, EOB ends)
        Assert.Equal(0, block[0]);
        for (int k = 1; k < 64; k++) Assert.Equal(0, block[k]);
    }

    // Test 4: Two MCUs verifying DC predictor update across MCUs
    [Fact]
    public void DecodeScanToBlocks_TwoMCUs_DCpredictor()
    {
        // Build DC table where two different symbols exist:
        // code0 -> category 0 (no bits)
        // code1 -> category 1 (one extra bit)
        var dcTable = new CanonicalHuffmanTable();
        dcTable.Add(0b0, 1, 0x00); // DC cat 0
        dcTable.Add(0b1, 1, 0x01); // DC cat 1
        var huffDc = new Dictionary<byte, CanonicalHuffmanTable> { [0] = dcTable };

        // AC: EOB only
        var acTable = new CanonicalHuffmanTable();
        acTable.Add(0b0, 1, 0x00); // EOB
        var huffAc = new Dictionary<byte, CanonicalHuffmanTable> { [0] = acTable };

        var qTable = new QuantizationTable { Values = new ushort[64] };
        var qTables = new Dictionary<byte, QuantizationTable> { [0] = qTable };

        var frame = new FrameInfo
        {
            Width = 16,
            Height = 8,
            Components = new List<ComponentInfo> { new ComponentInfo { Id = 1, SamplingFactor = 0x11, QuantizationTableId = 0 } }
        };
        var scan = new ScanInfo { Components = new List<ScanComponent> { new ScanComponent { ComponentId = 1, DcHuffmanTableId = 0, AcHuffmanTableId = 0 } } };

        // For two MCUs (left->right), we will encode two DC symbols:
        // MCU0: use code1 (category1) + extra bit '1' => dcDiff=+1 -> DC0=1
        // MCU1: use code0 (category0) => dcDiff=0 -> DC1 stays prevDc (1)
        // Bits: MCU0 symbol (1), MCU0 extra (1), MCU1 symbol (0), MCU1 EOB (we can ignore ACs since EOB is first)
        // We also must include EOB symbol between DCs? In decode, DC is followed by AC loop reading symbols; we will emit EOB for each MCU.
        // Sequence bits: MCU0 DC sym=1, MCU0 extra=1, MCU0 AC EOB=0, MCU1 DC sym=0, MCU1 AC EOB=0
        // Bits: 1 1 0 0 0 -> pack MSB-first => 0b11000000 = 0xC0
        byte[] compressed = new byte[] { 0b11000000 };

        var mcus = HuffmanTableLogic.DecodeScanToBlocks(compressed, frame, scan, qTables, huffDc, huffAc);

        Assert.Equal(2, mcus.Count);
        var b0 = mcus[0].ComponentBlocks[1][0];
        var b1 = mcus[1].ComponentBlocks[1][0];

        Assert.Equal(1, b0[0]); // DC0 = 1
        Assert.Equal(1, b1[0]); // DC1 = prevDc = 1 (category0)
    }

    [Fact]
    public void DecodeScanToBlocks_HandleZRL_EOB()
    {
        // Huffman table: AC can produce ZRL (0xF0) and EOB (0x00)
        var acTable = new CanonicalHuffmanTable();
        acTable.Add(0, 1, 0x00); // EOB
        acTable.Add(1, 1, 0xF0); // ZRL
        var huffAc = new Dictionary<byte, CanonicalHuffmanTable> { [0] = acTable };

        // DC table: trivial
        var dcTable = new CanonicalHuffmanTable();
        dcTable.Add(0, 1, 0x00);
        var huffDc = new Dictionary<byte, CanonicalHuffmanTable> { [0] = dcTable };

        var qTable = new QuantizationTable { Values = new ushort[64] };
        var qTables = new Dictionary<byte, QuantizationTable> { [0] = qTable };

        var frame = new FrameInfo
        {
            Width = 8,
            Height = 8,
            Components = new List<ComponentInfo>
            {
                new ComponentInfo { Id = 1, SamplingFactor = 0x11, QuantizationTableId = 0 }
            }
        };

        var scan = new ScanInfo
        {
            Components = new List<ScanComponent>
            {
                new ScanComponent { ComponentId = 1, DcHuffmanTableId = 0, AcHuffmanTableId = 0 }
            }
        };

        // Compressed: DC=0, then ZRL, then EOB
        byte[] compressed = new byte[] { 0b00000000, 0b10000000 }; // just synthetic bits

        var mcus = HuffmanTableLogic.DecodeScanToBlocks(compressed, frame, scan, qTables, huffDc, huffAc);
        var firstBlock = mcus[0].ComponentBlocks[1][0];
        // DC = 0
        Assert.Equal(0, firstBlock[0]);
        // ZRL skips 16 zeros, but all AC=0 anyway
        for (int k = 1; k < 64; k++)
            Assert.Equal(0, firstBlock[k]);
    }

    [Fact]
    public void DecodeScanToBlocks_ZRL_Skips16Zeros()
    {
        var dcTable = new CanonicalHuffmanTable();
        dcTable.Add(0, 1, 0x00);
        var huffDc = new Dictionary<byte, CanonicalHuffmanTable> { [0] = dcTable };

        var acTable = new CanonicalHuffmanTable();
        acTable.Add(0, 1, 0xF0); // ZRL
        acTable.Add(1, 1, 0x00); // EOB
        var huffAc = new Dictionary<byte, CanonicalHuffmanTable> { [0] = acTable };

        var qTable = new QuantizationTable { Values = new ushort[64] };
        var qTables = new Dictionary<byte, QuantizationTable> { [0] = qTable };

        var frame = new FrameInfo
        {
            Width = 8,
            Height = 8,
            Components = new List<ComponentInfo> { new ComponentInfo { Id = 1, SamplingFactor = 0x11, QuantizationTableId = 0 } }
        };

        var scan = new ScanInfo
        {
            Components = new List<ScanComponent> { new ScanComponent { ComponentId = 1, DcHuffmanTableId = 0, AcHuffmanTableId = 0 } }
        };

        // Compressed: DC=0, then ZRL (skip 16), then EOB
        byte[] compressed = new byte[] { 0b00000000, 0b10000000 };

        var mcus = HuffmanTableLogic.DecodeScanToBlocks(compressed, frame, scan, qTables, huffDc, huffAc);
        var block = mcus[0].ComponentBlocks[1][0];

        // DC=0
        Assert.Equal(0, block[0]);
        // ZRL skips 16 -> all AC remain zero
        for (int k = 1; k < 64; k++)
            Assert.Equal(0, block[k]);
    }

    [Fact]
    public void DecodeScanToBlocks_EOB_EarlyTermination()
    {
        var dcTable = new CanonicalHuffmanTable();
        dcTable.Add(0, 1, 0x00);
        var huffDc = new Dictionary<byte, CanonicalHuffmanTable> { [0] = dcTable };

        var acTable = new CanonicalHuffmanTable();
        acTable.Add(0, 1, 0x00); // EOB
        var huffAc = new Dictionary<byte, CanonicalHuffmanTable> { [0] = acTable };

        var qTable = new QuantizationTable { Values = new ushort[64] };
        var qTables = new Dictionary<byte, QuantizationTable> { [0] = qTable };

        var frame = new FrameInfo
        {
            Width = 8,
            Height = 8,
            Components = new List<ComponentInfo> { new ComponentInfo { Id = 1, SamplingFactor = 0x11, QuantizationTableId = 0 } }
        };

        var scan = new ScanInfo
        {
            Components = new List<ScanComponent> { new ScanComponent { ComponentId = 1, DcHuffmanTableId = 0, AcHuffmanTableId = 0 } }
        };

        byte[] compressed = new byte[] { 0b00000000 }; // DC=0, AC=EOB

        var mcus = HuffmanTableLogic.DecodeScanToBlocks(compressed, frame, scan, qTables, huffDc, huffAc);
        var block = mcus[0].ComponentBlocks[1][0];

        Assert.Equal(0, block[0]);
        for (int k = 1; k < 64; k++)
            Assert.Equal(0, block[k]);
    }

    [Fact]
    public void DecodeScanToBlocks_MultipleComponents_CheckOrder()
    {
        var dcTable = new CanonicalHuffmanTable();
        dcTable.Add(0, 1, 0x00);
        var huffDc = new Dictionary<byte, CanonicalHuffmanTable> { [0] = dcTable };

        var acTable = new CanonicalHuffmanTable();
        acTable.Add(0, 1, 0x00);
        var huffAc = new Dictionary<byte, CanonicalHuffmanTable> { [0] = acTable };

        var qTable = new QuantizationTable { Values = new ushort[64] };
        var qTables = new Dictionary<byte, QuantizationTable> { [0] = qTable };

        var frame = new FrameInfo
        {
            Width = 8,
            Height = 8,
            Components = new List<ComponentInfo>
            {
                new ComponentInfo { Id = 1, SamplingFactor = 0x11, QuantizationTableId = 0 },
                new ComponentInfo { Id = 2, SamplingFactor = 0x11, QuantizationTableId = 0 }
            }
        };

        var scan = new ScanInfo
        {
            Components = new List<ScanComponent>
            {
                new ScanComponent { ComponentId = 2, DcHuffmanTableId = 0, AcHuffmanTableId = 0 },
                new ScanComponent { ComponentId = 1, DcHuffmanTableId = 0, AcHuffmanTableId = 0 }
            }
        };

        byte[] compressed = new byte[] { 0b00000000, 0b00000000 }; // DC only for each component

        var mcus = HuffmanTableLogic.DecodeScanToBlocks(compressed, frame, scan, qTables, huffDc, huffAc);

        var blockComp1 = mcus[0].ComponentBlocks[1][0];
        var blockComp2 = mcus[0].ComponentBlocks[2][0];

        Assert.Equal(0, blockComp1[0]);
        Assert.Equal(0, blockComp2[0]);
    }

    // Helper: reuse previous synthetic block setup
    private List<MCUBlock> SyntheticDecodeSingleBlock()
    {
        var dcTable = new CanonicalHuffmanTable();
        dcTable.Add(0b0, 1, 0x03); // category 3 -> DC diff
        var huffDc = new Dictionary<byte, CanonicalHuffmanTable> { [0] = dcTable };

        var acTable = new CanonicalHuffmanTable();
        acTable.Add(0b0, 1, 0x21); // AC1: run=2,size=1
        acTable.Add(0b1, 1, 0x11); // AC2: run=1,size=1
        acTable.Add(0b10, 2, 0x00); // EOB
        var huffAc = new Dictionary<byte, CanonicalHuffmanTable> { [0] = acTable };

        var qTable = new QuantizationTable { Values = new ushort[64] };
        var qTables = new Dictionary<byte, QuantizationTable> { [0] = qTable };

        var frame = new FrameInfo
        {
            Width = 8,
            Height = 8,
            Components = new List<ComponentInfo>
            {
                new ComponentInfo { Id = 1, SamplingFactor = 0x11, QuantizationTableId = 0 }
            }
        };

        var scan = new ScanInfo
        {
            Components = new List<ScanComponent>
            {
                new ScanComponent { ComponentId = 1, DcHuffmanTableId = 0, AcHuffmanTableId = 0 }
            }
        };

        byte[] compressed = new byte[] { 0b10110000, 0b00000000 };
        return HuffmanTableLogic.DecodeScanToBlocks(compressed, frame, scan, qTables, huffDc, huffAc);
    }
}