using Image.Otp.Models.Jpeg;

namespace Image.Otp.SixLabors;

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
        if (frameInfo == null) throw new ArgumentNullException(nameof(frameInfo));
        if (scanInfo == null) throw new ArgumentNullException(nameof(scanInfo));
        if (compressedData == null) throw new ArgumentNullException(nameof(compressedData));
        if (qTables == null) throw new ArgumentNullException(nameof(qTables));
        if (frameInfo.Precision != 8) throw new NotSupportedException("Only 8-bit precision supported in this implementation.");

        int width = frameInfo.Width;
        int height = frameInfo.Height;
        int outputStride = width * 4; // RGBA

        // Prepare output buffer, filled with transparent black then alpha set to 255 per spec (we'll set RGB as we write)
        byte[] output = new byte[width * height * 4];
        for (int i = 0; i < width * height; i++)
        {
            int idx = i * 4;
            output[idx + 0] = 0;
            output[idx + 1] = 0;
            output[idx + 2] = 0;
            output[idx + 3] = 255; // straight alpha
        }

        // Determine MCU layout: MCU size in blocks = max sampling factors
        byte maxH = 1;
        byte maxV = 1;
        foreach (var comp in frameInfo.Components)
        {
            if (comp.HorizontalSampling > maxH) maxH = comp.HorizontalSampling;
            if (comp.VerticalSampling > maxV) maxV = comp.VerticalSampling;
        }

        int mcuWidthPixels = maxH * 8;
        int mcuHeightPixels = maxV * 8;

        // For each MCU in the list, place component blocks into output
        foreach (var mcu in compressedData)
        {
            // Top-left pixel of this MCU in image pixel coordinates
            int mcuOriginX = mcu.X * mcuWidthPixels;
            int mcuOriginY = mcu.Y * mcuHeightPixels;

            // For each scan component (scan order), find matching frame component and its sampling
            foreach (var scanComp in scanInfo.Components)
            {
                // Find component info in frame
                ComponentInfo compInfo = frameInfo.Components.FirstOrDefault(c => c.Id == scanComp.ComponentId);
                if (compInfo == null) continue; // skip if not found

                // Determine how many blocks horizontally and vertically for this component inside MCU
                int compH = compInfo.HorizontalSampling;
                int compV = compInfo.VerticalSampling;

                // Get quantization table for this component
                if (!qTables.TryGetValue(compInfo.QuantizationTableId, out QuantizationTable qTable))
                {
                    throw new InvalidOperationException($"Quantization table {compInfo.QuantizationTableId} not found.");
                }

                // Get the blocks for this component in the MCU
                if (!mcu.ComponentBlocks.TryGetValue(compInfo.Id, out List<short[]> blocksForComp))
                {
                    // No blocks for this component in this MCU (shouldn't happen normally) — skip.
                    continue;
                }

                // Raster order: iterate v then h to place each block inside MCU
                int blockIndex = 0;
                for (int by = 0; by < compV; by++)
                {
                    for (int bx = 0; bx < compH; bx++)
                    {
                        if (blockIndex >= blocksForComp.Count) break;
                        short[] quantizedCoeffs = blocksForComp[blockIndex++];
                        if (quantizedCoeffs == null || quantizedCoeffs.Length != 64)
                        {
                            // Invalid block — skip
                            continue;
                        }

                        if (quantizedCoeffs[1] > 0)
                        {

                        }

                        // Dequantize
                        double[] dequant = DequantizeBlock(quantizedCoeffs, qTable);

                        // Inverse DCT (8x8) -> yields a block of sample values (centered at 0 in JPEG; we'll add 128 after IDCT)
                        double[] samples = InverseDCT8x8(dequant);

                        // Convert component sample coordinates to image pixel coordinates.
                        // For subsampled components we need to upsample to MCU pixel grid.
                        // Each component block maps to an 8x8 region in the component's own sampling grid;
                        // the scale factor to full image pixels is (maxH/compH) horizontally and (maxV/compV) vertically.
                        int scaleX = maxH / compH;
                        int scaleY = maxV / compV;

                        int blockOriginX = mcuOriginX + bx * 8 * scaleX;
                        int blockOriginY = mcuOriginY + by * 8 * scaleY;

                        // For simplicity and clarity: nearest-neighbor upsampling from component samples to full-resolution pixels.
                        // samples[] are 8x8 in row-major (r*8 + c), values are in range roughly [-128,127]; add 128 to shift to [0..255]
                        // We'll handle Y/Cb/Cr differently later depending on component id; we need to accumulate values for each pixel across components.
                        // To do this cleanly, we can compose full-resolution temporary arrays for the MCU region: Y, Cb, Cr as needed.
                        // However to keep memory small and code simple, we'll store the upsampled block in a local 2D array for this block and then write/merge into final image.
                        for (int sy = 0; sy < 8; sy++)
                        {
                            for (int sx = 0; sx < 8; sx++)
                            {
                                double sampleValue = samples[sy * 8 + sx] + 128.0;
                                // clamp to 0..255
                                int sampleInt = (int)Math.Round(sampleValue);
                                if (sampleInt < 0) sampleInt = 0;
                                if (sampleInt > 255) sampleInt = 255;

                                // Upsample by writing the sampleInt into each corresponding output pixel in the scaleX x scaleY cell.
                                for (int uy = 0; uy < scaleY; uy++)
                                {
                                    int outY = blockOriginY + sy * scaleY + uy;
                                    if (outY < 0 || outY >= height) continue; // clip outside image
                                    for (int ux = 0; ux < scaleX; ux++)
                                    {
                                        int outX = blockOriginX + sx * scaleX + ux;
                                        if (outX < 0 || outX >= width) continue; // clip outside image

                                        // We need to merge this component's value with other components per pixel.
                                        // Approach: read existing RGBA pixel, but we don't have separated Y/Cb/Cr stored.
                                        // Instead, to keep code simple and readable: build three temporary per-pixel component planes for this MCU region.
                                        // However to avoid larger changes, we'll implement a simple per-pixel accumulation buffer keyed by image coords in a dictionary for MCU.
                                        // For clarity in this implementation, treat component IDs: 1=Y, 2=Cb, 3=Cr (common case). We will attempt to detect them.
                                        // We'll store per-pixel Y/Cb/Cr in a small dictionary keyed by int (outY * width + outX).
                                        int pixelIndex = outY * width + outX;
                                        PerPixelAccumulator.AddSample(pixelIndex, compInfo.Id, (byte)sampleInt);
                                    }
                                }
                            }
                        }
                    }
                } // end iter blocks for component
            } // end iter scan components

            // After processing all components in MCU, flush accumulated per-pixel YCbCr -> RGB into output buffer
            foreach (var kv in PerPixelAccumulator.FlushAndGetSamples())
            {
                int pixelIndex = kv.Key;
                var samples = kv.Value; // dictionary componentId -> byte value

                // Determine Y, Cb, Cr: common JPEG uses component ids 1=Y,2=Cb,3=Cr. If different, try best-effort by presence.
                byte yVal = 128;
                byte cbVal = 128;
                byte crVal = 128;

                if (samples.TryGetValue(1, out byte yv)) yVal = yv;
                else
                {
                    // try first available as luma fallback
                    foreach (var sv in samples.Values) { yVal = sv; break; }
                }

                if (samples.TryGetValue(2, out byte cbv)) cbVal = cbv;
                else if (samples.TryGetValue(3, out byte crvFallback))
                {
                    // if only two components present, guess neutral chroma
                    cbVal = 128;
                }

                if (samples.TryGetValue(3, out byte crv)) crVal = crv;
                else if (samples.TryGetValue(2, out byte cbvFallback))
                {
                    crVal = 128;
                }

                // Convert YCbCr to RGB (full range 0..255). Using ITU-R BT.601 conversion (standard JPEG).
                // R = Y + 1.402 * (Cr - 128)
                // G = Y - 0.344136 * (Cb - 128) - 0.714136 * (Cr - 128)
                // B = Y + 1.772 * (Cb - 128)
                double Yd = yVal;
                double Cbd = cbVal - 128.0;
                double Crd = crVal - 128.0;

                int r = (int)Math.Round(Yd + 1.402 * Crd);
                int g = (int)Math.Round(Yd - 0.344136 * Cbd - 0.714136 * Crd);
                int b = (int)Math.Round(Yd + 1.772 * Cbd);

                r = ClampToByte(r);
                g = ClampToByte(g);
                b = ClampToByte(b);

                int outIdx = pixelIndex * 4;
                output[outIdx + 0] = (byte)r;
                output[outIdx + 1] = (byte)g;
                output[outIdx + 2] = (byte)b;
                output[outIdx + 3] = 255;
            }

            // Clear per-MCU accumulator for next MCU
            PerPixelAccumulator.Clear();
        } // end foreach MCU

        return output;
    }

    private static double[] DequantizeBlock(short[] quantizedCoeffs, QuantizationTable qTable)
    {
        // quantizedCoeffs is in zig-zag or natural order? We'll assume natural-order 8x8 (0..63) since DecodedMCU likely provided natural order.
        // If zig-zag ordering is used externally, this function would need a zig-zag map. For clarity we assume natural order here.
        double[] outBlock = new double[64];
        for (int i = 0; i < 64; i++)
        {
            ushort qv = (i < qTable.Values.Length) ? qTable.Values[i] : (ushort)1;
            outBlock[i] = quantizedCoeffs[i] * (double)qv;
        }
        return outBlock;
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

