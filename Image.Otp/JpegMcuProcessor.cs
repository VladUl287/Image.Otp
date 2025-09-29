using Image.Otp.Core.Models.Jpeg;
using System.Buffers;

namespace Image.Otp;

public static class JpegProcessor
{
    public static byte[] ProcessMCUBlocks(
        FrameInfo frameInfo,
        List<MCUBlock> compressedData,
        Dictionary<byte, QuantizationTable> qTables)
    {
        if (frameInfo == null) throw new ArgumentNullException(nameof(frameInfo));
        if (compressedData == null) throw new ArgumentNullException(nameof(compressedData));
        if (qTables == null) throw new ArgumentNullException(nameof(qTables));
        if (frameInfo.Precision != 8) throw new NotSupportedException("Only 8-bit precision supported.");

        int width = frameInfo.Width;
        int height = frameInfo.Height;
        var output = new byte[width * height * 4];

        // Initialize alpha and RGB to 0 (alpha=255)
        for (int i = 0; i < width * height; i++)
        {
            int off = i * 4;
            output[off + 0] = 0;
            output[off + 1] = 0;
            output[off + 2] = 0;
            output[off + 3] = 255;
        }

        // Find maximum sampling factors (MCU block grid)
        int maxH = 1, maxV = 1;
        foreach (var c in frameInfo.Components)
        {
            if (c.HorizontalSampling > maxH) maxH = c.HorizontalSampling;
            if (c.VerticalSampling > maxV) maxV = c.VerticalSampling;
        }
        int mcuWidthPixels = maxH * 8;
        int mcuHeightPixels = maxV * 8;

        // Map componentId -> ComponentInfo for quick lookup
        var compById = frameInfo.Components.ToDictionary(c => c.Id);

        // Process each MCU
        foreach (var mcu in compressedData)
        {
            int mcuOriginX = mcu.X * mcuWidthPixels;
            int mcuOriginY = mcu.Y * mcuHeightPixels;

            // Local per-MCU accumulator: pixelIndex -> dictionary(componentId -> sampleByte)
            // Use int pixelIndex = outY * width + outX
            var pixelAccumulator = new Dictionary<int, Dictionary<byte, byte>>();

            // For each component present in this MCU's blocks (not using scanInfo)
            foreach (var kv in mcu.ComponentBlocks)
            {
                byte compId = kv.Key;
                var blocksForComp = kv.Value; // List<short[]>

                if (!compById.TryGetValue(compId, out var compInfo))
                {
                    // Unknown component id: skip
                    continue;
                }

                // Get quant table
                if (!qTables.TryGetValue(compInfo.QuantizationTableId, out var qTable))
                {
                    throw new InvalidOperationException($"Quantization table {compInfo.QuantizationTableId} not found.");
                }

                int compH = compInfo.HorizontalSampling;
                int compV = compInfo.VerticalSampling;

                int scaleX = maxH / compH;
                int scaleY = maxV / compV;

                int blockIndex = 0;
                for (int by = 0; by < compV; by++)
                {
                    for (int bx = 0; bx < compH; bx++)
                    {
                        if (blockIndex >= blocksForComp.Count) break; // defensive
                        var quantizedCoeffs = blocksForComp[blockIndex++];
                        if (quantizedCoeffs == null || quantizedCoeffs.Length != 64) continue;

                        // 1) Dequantize
                        double[] dequant = DequantizeBlock(quantizedCoeffs, qTable); // returns 64 doubles

                        // 2) IDCT -> 8x8 samples, centered around 0; later add 128
                        double[] samples = InverseDCT8x8(dequant); // length 64

                        // 3) Map block to pixel origin (with upsampling)
                        int blockOriginX = mcuOriginX + bx * 8 * scaleX;
                        int blockOriginY = mcuOriginY + by * 8 * scaleY;

                        for (int sy = 0; sy < 8; sy++)
                        {
                            for (int sx = 0; sx < 8; sx++)
                            {
                                double sampleValue = samples[sy * 8 + sx] + 128.0;
                                int sampleInt = (int)Math.Round(sampleValue);
                                if (sampleInt < 0) sampleInt = 0;
                                if (sampleInt > 255) sampleInt = 255;
                                byte sampleByte = (byte)sampleInt;

                                // Upsample into scaleX x scaleY pixel block
                                for (int uy = 0; uy < scaleY; uy++)
                                {
                                    int outY = blockOriginY + sy * scaleY + uy;
                                    if (outY < 0 || outY >= height) continue;
                                    for (int ux = 0; ux < scaleX; ux++)
                                    {
                                        int outX = blockOriginX + sx * scaleX + ux;
                                        if (outX < 0 || outX >= width) continue;

                                        int pixelIndex = outY * width + outX;
                                        if (!pixelAccumulator.TryGetValue(pixelIndex, out var compSamples))
                                        {
                                            compSamples = new Dictionary<byte, byte>();
                                            pixelAccumulator[pixelIndex] = compSamples;
                                        }

                                        // store latest sample value for this component id; if multiple blocks write same pixel, last write wins (OK for nearest-neighbor)
                                        compSamples[compId] = sampleByte;
                                    }
                                }
                            }
                        }
                    }
                }
            } // end foreach component in mcu

            // Flush accumulator for this MCU into final RGBA buffer
            foreach (var pair in pixelAccumulator)
            {
                int pixelIndex = pair.Key;
                var compSamples = pair.Value;

                // Determine Y, Cb, Cr from compSamples using frame component ids.
                // Default chroma neutral = 128 when missing.
                byte yVal = 128, cbVal = 128, crVal = 128;

                // Try find a Y component (common id is 1, but we consult frameInfo)
                // We'll look for the first component which has sampling matching typical luminance heuristic:
                // but simpler: if frame has a single component, use that as Y; otherwise prefer component with largest sampling (usually Y).
                // To keep it robust, we try to find the component with the largest sampling as Y fallback.
                // First try exact known ids:
                if (compSamples.Count == 1)
                {
                    // only one component present; use it as Y
                    foreach (var v in compSamples.Values) { yVal = v; break; }
                }
                else
                {
                    // prefer component id that exists in frameInfo with max sampling (likely Y)
                    // build list of available frame components in this pixel (comp id -> sampling)
                    byte bestYId = 0; int bestSampling = -1;
                    foreach (var cid in compSamples.Keys)
                    {
                        if (!compById.TryGetValue(cid, out var cf)) continue;
                        int sampling = cf.HorizontalSampling * cf.VerticalSampling;
                        if (sampling > bestSampling)
                        {
                            bestSampling = sampling;
                            bestYId = cid;
                        }
                    }
                    if (bestYId != 0 && compSamples.TryGetValue(bestYId, out var yb)) yVal = yb;

                    // try to set chroma by presence of other typical ids
                    // If we have any two components, pick first other for Cb and next for Cr (best-effort)
                    var keys = compSamples.Keys.ToList();
                    // remove chosen y id from keys
                    keys.RemoveAll(k => k == bestYId);

                    if (keys.Count >= 1)
                    {
                        cbVal = compSamples[keys[0]];
                    }
                    if (keys.Count >= 2)
                    {
                        crVal = compSamples[keys[1]];
                    }

                    // If we still have specific comp ids (1,2,3), prefer them:
                    if (compSamples.TryGetValue(1, out var s1)) yVal = s1;
                    if (compSamples.TryGetValue(2, out var s2)) cbVal = s2;
                    if (compSamples.TryGetValue(3, out var s3)) crVal = s3;
                }

                // Convert YCbCr -> RGB (BT.601)
                double Yd = yVal;
                double Cbd = cbVal - 128.0;
                double Crd = crVal - 128.0;
                int r = ClampToByte((int)Math.Round(Yd + 1.402 * Crd));
                int g = ClampToByte((int)Math.Round(Yd - 0.344136 * Cbd - 0.714136 * Crd));
                int b = ClampToByte((int)Math.Round(Yd + 1.772 * Cbd));

                int outIdx = pixelIndex * 4;
                output[outIdx + 0] = (byte)r;
                output[outIdx + 1] = (byte)g;
                output[outIdx + 2] = (byte)b;
                output[outIdx + 3] = 255;
            }

            // next MCU
        } // end foreach mcu

        return output;
    }

    public static byte[] ProcessMCUBlocks(
        FrameInfo frameInfo,
        ScanInfo scanInfo,
        List<MCUBlock> compressedData,
        Dictionary<byte, QuantizationTable> qTables)
    {
        ArgumentNullException.ThrowIfNull(frameInfo);
        ArgumentNullException.ThrowIfNull(scanInfo);
        ArgumentNullException.ThrowIfNull(compressedData);
        ArgumentNullException.ThrowIfNull(qTables);

        if (frameInfo.Precision != 8)
            throw new NotSupportedException("Only 8-bit precision supported in this implementation.");

        int width = frameInfo.Width;
        int height = frameInfo.Height;
        int outputStride = width * 4; // RGBA

        byte[] output = new byte[width * height * 4];
        Array.Fill(output, (byte)255); // Initialize alpha to 255

        // Pre-calculate component information
        byte maxH = 1;
        byte maxV = 1;
        foreach (var comp in frameInfo.Components)
        {
            if (comp.HorizontalSampling > maxH) maxH = comp.HorizontalSampling;
            if (comp.VerticalSampling > maxV) maxV = comp.VerticalSampling;
        }

        int mcuWidthPixels = maxH * 8;
        int mcuHeightPixels = maxV * 8;

        // Pre-allocate buffers for component processing
        var componentBuffers = new Dictionary<byte, byte[]>();
        foreach (var comp in frameInfo.Components)
        {
            componentBuffers[comp.Id] = new byte[width * height];
            Array.Fill(componentBuffers[comp.Id], (byte)128); // Initialize to mid-value
        }

        // Process each component separately and store in component buffers
        foreach (var comp in frameInfo.Components)
        {
            if (!qTables.TryGetValue(comp.QuantizationTableId, out var qTable))
            {
                throw new InvalidOperationException($"Quantization table {comp.QuantizationTableId} not found.");
            }

            int compH = comp.HorizontalSampling;
            int compV = comp.VerticalSampling;
            int scaleX = maxH / compH;
            int scaleY = maxV / compV;

            byte[] compBuffer = componentBuffers[comp.Id];

            foreach (var mcu in compressedData)
            {
                int mcuOriginX = mcu.X * mcuWidthPixels;
                int mcuOriginY = mcu.Y * mcuHeightPixels;

                if (!mcu.ComponentBlocks.TryGetValue(comp.Id, out var blocksForComp))
                    continue;

                int blockIndex = 0;
                for (int by = 0; by < compV; by++)
                {
                    for (int bx = 0; bx < compH; bx++)
                    {
                        if (blockIndex >= blocksForComp.Count)
                            break;

                        var quantizedCoeffs = blocksForComp[blockIndex++];
                        if (quantizedCoeffs is null or { Length: not 64 })
                            continue;

                        
                        double[] dequant = ArrayPool<double>.Shared.Rent(64);
                        DequantizeBlock(quantizedCoeffs, qTable, dequant);
                        double[] samples = ArrayPool<double>.Shared.Rent(64);
                        InverseDCT8x8(dequant, samples);

                        ArrayPool<double>.Shared.Return(dequant);

                        int blockOriginX = mcuOriginX + bx * 8 * scaleX;
                        int blockOriginY = mcuOriginY + by * 8 * scaleY;

                        for (int sy = 0; sy < 8; sy++)
                        {
                            for (int sx = 0; sx < 8; sx++)
                            {
                                double sampleValue = samples[sy * 8 + sx] + 128.0;
                                int sampleInt = (int)Math.Round(sampleValue);
                                sampleInt = Math.Clamp(sampleInt, 0, 255);

                                for (int uy = 0; uy < scaleY; uy++)
                                {
                                    int outY = blockOriginY + sy * scaleY + uy;
                                    if (outY < 0 || outY >= height) continue;
                                    for (int ux = 0; ux < scaleX; ux++)
                                    {
                                        int outX = blockOriginX + sx * scaleX + ux;
                                        if (outX < 0 || outX >= width) continue;

                                        int pixelIndex = outY * width + outX;
                                        compBuffer[pixelIndex] = (byte)sampleInt;
                                    }
                                }
                            }
                        }

                        ArrayPool<double>.Shared.Return(samples);
                    }
                }
            }
        }

        // Convert from YCbCr to RGB in a single pass
        byte[] yBuffer = componentBuffers[1];
        byte[] cbBuffer = componentBuffers.ContainsKey(2) ? componentBuffers[2] : null;
        byte[] crBuffer = componentBuffers.ContainsKey(3) ? componentBuffers[3] : null;

        for (int i = 0; i < width * height; i++)
        {
            byte yVal = yBuffer[i];
            byte cbVal = cbBuffer != null ? cbBuffer[i] : (byte)128;
            byte crVal = crBuffer != null ? crBuffer[i] : (byte)128;

            double Yd = yVal;
            double Cbd = cbVal - 128.0;
            double Crd = crVal - 128.0;

            int r = (int)Math.Round(Yd + 1.402 * Crd);
            int g = (int)Math.Round(Yd - 0.344136 * Cbd - 0.714136 * Crd);
            int b = (int)Math.Round(Yd + 1.772 * Cbd);

            r = Math.Clamp(r, 0, 255);
            g = Math.Clamp(g, 0, 255);
            b = Math.Clamp(b, 0, 255);

            int outIdx = i * 4;
            output[outIdx + 0] = (byte)r;
            output[outIdx + 1] = (byte)g;
            output[outIdx + 2] = (byte)b;
        }

        return output;
    }

    private static double[] DequantizeBlock(short[] quantizedCoeffs, QuantizationTable qTable, double[] outBlock)
    {
        for (int i = 0; i < 64; i++)
        {
            ushort qv = (i < qTable.Values.Length) ? qTable.Values[i] : (ushort)1;
            outBlock[i] = quantizedCoeffs[i] * (double)qv;
        }
        return outBlock;
    }

    private static double[] DequantizeBlock(short[] quantizedCoeffs, QuantizationTable qTable)
    {
        double[] outBlock = new double[64];
        for (int i = 0; i < 64; i++)
        {
            ushort qv = (i < qTable.Values.Length) ? qTable.Values[i] : (ushort)1;
            outBlock[i] = quantizedCoeffs[i] * (double)qv;
        }
        return outBlock;
    }

    private static void InverseDCT8x8(double[] block, double[] output)
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

    private static double[] InverseDCT8x8(double[] block)
    {
        // Implement basic separable IDCT using the naive formula for clarity.
        // This is slow but easy to read.
        double[] tmp = new double[64];
        double[] output = new double[64];

        // Precompute basis factors
        double c(int u) => u == 0 ? 1.0 / Math.Sqrt(2.0) : 1.0;

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

        return output;
    }

    private static int ClampToByte(int v)
    {
        if (v < 0) return 0;
        if (v > 255) return 255;
        return v;
    }

    private static class PerPixelAccumulator
    {
        // map pixelIndex -> (componentId -> value)
        private static Dictionary<int, Dictionary<byte, byte>> _map = new Dictionary<int, Dictionary<byte, byte>>();

        public static void AddSample(int pixelIndex, byte componentId, byte value)
        {
            if (!_map.TryGetValue(pixelIndex, out var compDict))
            {
                compDict = new Dictionary<byte, byte>();
                _map[pixelIndex] = compDict;
            }
            // In case multiple samples for same component (due to upsampling overlap), simple last-write-wins.
            compDict[componentId] = value;
        }

        public static Dictionary<int, Dictionary<byte, byte>> FlushAndGetSamples()
        {
            // Return shallow copy so the caller can iterate safely then Clear() will reset internal state.
            return new Dictionary<int, Dictionary<byte, byte>>(_map);
        }

        public static void Clear()
        {
            _map.Clear();
        }
    }
}

