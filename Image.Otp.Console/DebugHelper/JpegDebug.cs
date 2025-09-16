using System.Drawing;
using Image.Otp.Models.Jpeg;

namespace Image.Otp.Console.DebugHelper;

public static class JpegDebug
{
    // Diagnostic record for a single block emitted by producer
    public record BlockOwner(int BlockIndex, int McuX, int McuY, int CompId, int Vy, int Hx);

    // Build the sequence *actually emitted* by your producer (instrument this code path if possible).
    // If you can't instrument the producer, call BuildProducerSequenceFromMcus using the decoded MCUs.
    public static List<BlockOwner> BuildProducerSequenceFromMcus(
        List<MCUBlock> mcus,
        FrameInfo frameInfo,
        ScanInfo scanInfo)
    {
        // This reflects your DecodeScanToBlocks iteration:
        // Outer loop: MCU raster my,mx
        // Inner: foreach scanComp in scan.Components
        // For each component: blocks list of length H*V emitted in order (assumed vy major then hx)
        var seq = new List<BlockOwner>();
        int mcusX, mcusY;
        {
            int maxH = 1, maxV = 1;
            foreach (var c in frameInfo.Components)
            {
                int H = (c.SamplingFactor >> 4) & 0x0F;
                int V = c.SamplingFactor & 0x0F;
                if (H > maxH) maxH = H;
                if (V > maxV) maxV = V;
            }
            int mcuWidth = maxH * 8;
            int mcuHeight = maxV * 8;
            mcusX = (frameInfo.Width + mcuWidth - 1) / mcuWidth;
            mcusY = (frameInfo.Height + mcuHeight - 1) / mcuHeight;
        }

        int blockIndex = 0;
        for (int my = 0; my < mcusY; my++)
        {
            for (int mx = 0; mx < mcusX; mx++)
            {
                int mcuIndex = my * mcusX + mx;
                if (mcuIndex >= mcus.Count) break;
                var mcu = mcus[mcuIndex];

                foreach (var scanComp in scanInfo.Components)
                {
                    byte compId = scanComp.ComponentId;
                    var frameComp = frameInfo.Components.First(f => f.Id == compId);
                    int H = (frameComp.SamplingFactor >> 4) & 0x0F;
                    int V = frameComp.SamplingFactor & 0x0F;
                    if (!mcu.ComponentBlocks.TryGetValue(compId, out var blocks))
                    {
                        // missing, still record placeholders
                        for (int vy = 0; vy < V; vy++)
                            for (int hx = 0; hx < H; hx++)
                                seq.Add(new BlockOwner(blockIndex++, mx, my, compId, vy, hx));
                        continue;
                    }
                    int expected = H * V;
                    if (blocks.Count != expected)
                    {
                        System.Console.WriteLine($"WARNING: MCU({mx},{my}) comp {compId} has {blocks.Count} blocks but expected {expected}");
                    }
                    // Blocks are stored in blockList order — assume they correspond to vy,hx order
                    int b = 0;
                    for (int vy = 0; vy < V; vy++)
                    {
                        for (int hx = 0; hx < H; hx++)
                        {
                            seq.Add(new BlockOwner(blockIndex++, mx, my, compId, vy, hx));
                            b++;
                        }
                    }
                }
            }
        }
        return seq;
    }

    // Build what PostProcess *expects* given an assumption about component order used by allBlocks.
    // orderingMode: "scan" or "frame" indicates which component order the producer used.
    public static List<BlockOwner> BuildExpectedSequence(
    FrameInfo frameInfo,
    ScanInfo scanInfo, string orderingMode) // "scan" or "frame"

    {
        // compute mcusX/mcusY using same logic as your postprocess
        int maxH = frameInfo.Components.Max(c => (c.SamplingFactor >> 4) & 0x0F);
        int maxV = frameInfo.Components.Max(c => c.SamplingFactor & 0x0F);
        int mcuWidth = maxH * 8;
        int mcuHeight = maxV * 8;
        int mcusX = (frameInfo.Width + mcuWidth - 1) / mcuWidth;
        int mcusY = (frameInfo.Height + mcuHeight - 1) / mcuHeight;

        var seq = new List<BlockOwner>();
        int blockIndex = 0;

        for (int my = 0; my < mcusY; my++)
        {
            for (int mx = 0; mx < mcusX; mx++)
            {
                IEnumerable<ComponentInfo> comps;
                if (orderingMode == "scan") comps = scanInfo.Components.Select(sc => frameInfo.Components.First(f => f.Id == sc.ComponentId));
                else comps = frameInfo.Components;

                foreach (var comp in comps)
                {
                    int H = (comp.SamplingFactor >> 4) & 0x0F;
                    int V = comp.SamplingFactor & 0x0F;
                    for (int vy = 0; vy < V; vy++)
                        for (int hx = 0; hx < H; hx++)
                            seq.Add(new BlockOwner(blockIndex++, mx, my, comp.Id, vy, hx));
                }
            }
        }
        return seq;
    }

    // Compare two sequences and print first N mismatches
    public static void CompareSequences(List<BlockOwner> producer, List<BlockOwner> expected, int maxPrint = 50)
    {
        int n = Math.Min(Math.Min(producer.Count, expected.Count), Math.Max(maxPrint, 1));
        System.Console.WriteLine($"Producer.Count={producer.Count}, Expected.Count={expected.Count}");
        int mismatches = 0;
        int limit = Math.Min(Math.Max(producer.Count, expected.Count), maxPrint);
        for (int i = 0; i < limit; i++)
        {
            var p = i < producer.Count ? producer[i] : null;
            var e = i < expected.Count ? expected[i] : null;
            if (p == null || e == null || p.CompId != e.CompId || p.McuX != e.McuX || p.McuY != e.McuY || p.Vy != e.Vy || p.Hx != e.Hx)
            {
                mismatches++;
                System.Console.WriteLine($"Mismatch at index {i}: produced={FormatOwner(p)} expected={FormatOwner(e)}");
            }
            else
            {
                System.Console.WriteLine($"OK index {i}: {FormatOwner(p)}");
            }
        }
        if (mismatches == 0) System.Console.WriteLine("No mismatches in the first " + limit + " entries.");
        else System.Console.WriteLine($"Mismatches found: {mismatches} (first {limit} checked).");
    }

    private static string FormatOwner(BlockOwner o)
    {
        if (o == null) return "<null>";
        return $"idx={o.BlockIndex} mcu=({o.McuX},{o.McuY}) comp={o.CompId} vy={o.Vy} hx={o.Hx}";
    }

    // Render a mosaic image showing block indices according to a sequence mapping.
    // Each block becomes an 8x8 tile scaled by 'scale'.
    public static void RenderBlockIndexMosaic(string path, List<BlockOwner> sequence, FrameInfo frameInfo, string title = "")
    {
        int maxH = frameInfo.Components.Max(c => (c.SamplingFactor >> 4) & 0x0F);
        int maxV = frameInfo.Components.Max(c => c.SamplingFactor & 0x0F);
        int mcuWidth = maxH * 8;
        int mcuHeight = maxV * 8;
        int mcusX = (frameInfo.Width + mcuWidth - 1) / mcuWidth;
        int mcusY = (frameInfo.Height + mcuHeight - 1) / mcuHeight;

        int imageW = mcusX * mcuWidth;
        int imageH = mcusY * mcuHeight;
        int scale = 4; // pixel scale for readability
        using var bmp = new Bitmap(imageW * scale, imageH * scale);
        using var g = Graphics.FromImage(bmp);
        g.Clear(Color.Black);

        foreach (var owner in sequence)
        {
            // compute top-left pixel for this block in full-image coordinates (component native -> but we draw on full image grid)
            // We'll place block using (mcuX,mcuY,vy,hx) with maxH,maxV to compute full-image position:
            int blockFullX = (owner.McuX * maxH + owner.Hx) * 8;
            int blockFullY = (owner.McuY * maxV + owner.Vy) * 8;
            int px = blockFullX * scale;
            int py = blockFullY * scale;

            // draw rectangle & index
            var rect = new Rectangle(px, py, 8 * scale, 8 * scale);
            g.FillRectangle(Brushes.DarkSlateGray, rect);
            g.DrawRectangle(Pens.White, rect);
            var f = new Font("Arial", 6 * scale / 4);
            g.DrawString(owner.BlockIndex.ToString(), f, Brushes.Yellow, px + 1, py + 1);
        }

        if (!string.IsNullOrEmpty(title))
            g.DrawString(title, new Font("Arial", 12), Brushes.Lime, 2, 2);

        Directory.CreateDirectory(Path.GetDirectoryName(path));
        bmp.Save(path);
        System.Console.WriteLine("Wrote mosaic: " + path);
    }
}