using Image.Otp.Models.Jpeg;
using System.Buffers;
using System.Text;

namespace Image.Otp.Helpers;

public static class JpegHelpres
{
    public static double[] DequantizeBlock(short[] quantizedCoeffs, QuantizationTable qTable)
    {
        double[] outBlock = new double[64];
        for (int i = 0; i < 64; i++)
        {
            ushort qv = (i < qTable.Values.Length) ? qTable.Values[i] : (ushort)1;
            outBlock[i] = quantizedCoeffs[i] * (double)qv;
        }
        return outBlock;
    }

    public static void InverseDCT8x8(double[] block, double[] output)
    {
        // Implement basic separable IDCT using the naive formula for clarity.
        // This is slow but easy to read.
        double[] tmp = ArrayPool<double>.Shared.Rent(64);

        // Precompute basis factors
        static double c(int u) => u == 0 ? 1.0 / Math.Sqrt(2.0) : 1.0;

        // 1D IDCT on rows
        for (int y = 0; y < 8; y++)
        {
            for (int x = 0; x < 8; x++)
            {
                double sum = 0.0;
                for (int u = 0; u < 8; u++)
                {
                    sum += c(u) * block[y * 8 + u] * Math.Cos(((2 * x + 1) * u * Math.PI) / 16.0);
                }
                tmp[y * 8 + x] = sum * 0.5;
            }
        }

        // 1D IDCT on columns
        for (int x = 0; x < 8; x++)
        {
            for (int y = 0; y < 8; y++)
            {
                double sum = 0.0;
                for (int v = 0; v < 8; v++)
                {
                    sum += c(v) * tmp[v * 8 + x] * Math.Cos(((2 * y + 1) * v * Math.PI) / 16.0);
                }
                output[y * 8 + x] = sum * 0.5;
            }
        }

        ArrayPool<double>.Shared.Return(tmp);
    }

    public static int FindNextMarker(StreamBitReader br)
    {
        while (true)
        {
            int b = br.ReadRawByte();
            if (b < 0) 
                return -1;

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

    public static int DecodeHuffmanSymbol(StreamBitReader br, CanonicalHuffmanTable table)
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

    public static void PrintAllComponentsLikeC(List<MCUBlock> allMcus)
    {
        var file = new StringBuilder(1_000_000);

        // First, organize blocks by component instead of by MCU
        var components = new Dictionary<byte, List<(int mcuX, int mcuY, short[] block)>>();

        foreach (var mcu in allMcus)
        {
            foreach (var component in mcu.ComponentBlocks)
            {
                byte componentId = component.Key;
                if (!components.ContainsKey(componentId))
                {
                    components[componentId] = new List<(int, int, short[])>();
                }

                for (int blockIndex = 0; blockIndex < component.Value.Count; blockIndex++)
                {
                    components[componentId].Add((mcu.X, mcu.Y, component.Value[blockIndex]));
                }
            }
        }

        // Now print in component order like the C code
        foreach (var component in components.OrderBy(c => c.Key))
        {
            byte componentId = component.Key;
            var blocks = component.Value;

            int maxX = blocks.Max(b => b.mcuX);
            int maxY = blocks.Max(b => b.mcuY);
            int blocksPerMCU = blocks.Count / ((maxX + 1) * (maxY + 1));

            file.AppendLine($"Component {componentId - 1} blocks: {maxX + 1} x {maxY + 1}");

            for (int y = 0; y <= maxY; y++)
            {
                for (int x = 0; x <= maxX; x++)
                {
                    // Find the block at this position
                    var block = blocks.FirstOrDefault(b => b.mcuX == x && b.mcuY == y);

                    if (block.block != null)
                    {
                        //var blockblock = ZigZagToNatural(block.block);
                        var blockblock = block.block;
                        file.AppendLine($"Comp {componentId - 1} block ({x},{y}):");

                        // Print the 8x8 block in the exact same format as C
                        for (int k = 0; k < 64; k++)
                        {
                            if (k % 8 == 7)
                            {
                                file.AppendLine($"{blockblock[k]}");
                            }
                            else
                            {
                                file.Append($"{blockblock[k]} ");
                            }
                        }
                    }
                }
            }
        }

        File.WriteAllText("C:\\Users\\User\\source\\repos\\dump\\test.txt", file.ToString());
    }

    public static bool IsProgressiveJpeg(byte[] jpegBytes)
    {
        if (jpegBytes == null || jpegBytes.Length < 4) return false;

        int idx = 0;
        // Check SOI marker 0xFFD8
        if (jpegBytes[idx++] != 0xFF || jpegBytes[idx++] != 0xD8) return false;

        while (idx + 1 < jpegBytes.Length)
        {
            // Skip any 0xFF fill bytes
            if (jpegBytes[idx] != 0xFF)
            {
                idx++;
                continue;
            }

            // Read marker
            int marker = jpegBytes[++idx] & 0xFF;
            idx++;

            // Standalone markers without length: 0xD0-0xD9 (RSTx), 0x01 (TEM)
            if (marker == 0xD8 || marker == 0xD9) break; // SOI or EOI: stop
            if (marker == 0x01 || (marker >= 0xD0 && marker <= 0xD9)) continue;

            // Need at least two bytes for length
            if (idx + 1 >= jpegBytes.Length) break;
            int length = (jpegBytes[idx] << 8) | jpegBytes[idx + 1];
            if (length < 2) return false; // corrupt
            // Move idx to start of payload
            idx += 2;

            // SOF0 = 0xC0 (baseline), SOF2 = 0xC2 (progressive)
            if (marker == 0xC0) return false; // explicitly baseline
            if (marker == 0xC2) return true;  // progressive

            // Skip payload
            idx += (length - 2);
        }

        return false;
    }

    public static bool HasRestartMarkers(byte[] data)
    {
        for (int i = 0; i + 1 < data.Length; i++)
        {
            if (data[i] == 0xFF)
            {
                int b = data[i + 1];
                if (b >= 0xD0 && b <= 0xD7)
                    return true;
                // skip stuffed 0x00 (0xFF 0x00), treat as data
                if (b == 0x00) i++; // skip the stuffed zero
            }
        }
        return false;
    }
}
