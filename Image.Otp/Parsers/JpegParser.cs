using Image.Otp.Core.Models.Jpeg;

namespace Image.Otp.Core.Parsers;

public sealed class JpegParser
{
    public static List<JpegSegment> ParseSegmentsWithRestartMarkers(byte[] bytes)
    {
        var segments = new List<JpegSegment>();
        int i = 0;
        bool inScan = false;
        int scanStart = 0;

        while (i < bytes.Length)
        {
            // Check if we're at a potential marker (0xFF)
            if (bytes[i] == 0xFF)
            {
                // Check if we have at least one more byte
                if (i + 1 >= bytes.Length)
                {
                    // Invalid - marker without type
                    i++;
                    continue;
                }

                byte markerType = bytes[i + 1];

                // Handle byte stuffing (0xFF00 in compressed data)
                if (inScan && markerType == 0x00)
                {
                    i += 2;
                    continue;
                }

                // Handle restart markers in compressed data
                if (inScan && markerType >= 0xD0 && markerType <= 0xD7)
                {
                    i += 2;
                    continue;
                }

                // If we're in a scan and encounter a non-restart marker, end the scan
                if (inScan)
                {
                    // Create compressed data segment
                    int compLen = i - scanStart;
                    byte[] compData = new byte[compLen];
                    Array.Copy(bytes, scanStart, compData, 0, compLen);

                    segments.Add(new JpegSegment
                    {
                        Offset = scanStart,
                        Marker = 0x00, // Custom marker for compressed data
                        Name = "CompressedData",
                        Length = compLen,
                        Data = compData
                    });

                    inScan = false;
                }

                // Handle markers with length
                if (HasLength(markerType))
                {
                    if (i + 3 >= bytes.Length)
                    {
                        // Not enough bytes for length
                        break;
                    }

                    int length = (bytes[i + 2] << 8) | bytes[i + 3];

                    if (i + 2 + length >= bytes.Length)
                    {
                        // Length extends beyond file
                        break;
                    }

                    byte[] data = new byte[length - 2];
                    if (length > 2)
                    {
                        Array.Copy(bytes, i + 4, data, 0, length - 2);
                    }

                    segments.Add(new JpegSegment
                    {
                        Offset = i,
                        Marker = markerType,
                        Name = GetMarkerName(markerType),
                        Length = length,
                        Data = data
                    });

                    // Check if this is a SOS marker - start of compressed data
                    if (markerType == 0xDA) // SOS
                    {
                        inScan = true;
                        scanStart = i + 2 + length;
                    }

                    i += 2 + length;
                }
                else
                {
                    // Handle markers without length
                    segments.Add(new JpegSegment
                    {
                        Offset = i,
                        Marker = markerType,
                        Name = GetMarkerName(markerType),
                        Length = 0,
                        Data = new byte[0]
                    });

                    // Check for EOI (end of image)
                    if (markerType == 0xD9) // EOI
                    {
                        break;
                    }

                    i += 2;
                }
            }
            else
            {
                // If we're in a scan, just continue
                if (inScan)
                {
                    i++;
                }
                else
                {
                    // Invalid JPEG - non-FF byte outside of scan
                    i++;
                }
            }
        }

        // Handle case where file ends during scan
        if (inScan)
        {
            int compLen = bytes.Length - scanStart;
            byte[] compData = new byte[compLen];
            Array.Copy(bytes, scanStart, compData, 0, compLen);

            segments.Add(new JpegSegment
            {
                Offset = scanStart,
                Marker = 0x00,
                Name = "CompressedData",
                Length = compLen,
                Data = compData
            });
        }

        return segments;
    }

    private static bool HasLength(byte marker)
    {
        // Markers that have length data
        switch (marker)
        {
            case 0xC0:
            case 0xC1:
            case 0xC2:
            case 0xC3:
            case 0xC5:
            case 0xC6:
            case 0xC7:
            case 0xC8:
            case 0xC9:
            case 0xCA:
            case 0xCB:
            case 0xCD:
            case 0xCE:
            case 0xCF: // SOF
            case 0xC4: // DHT
            case 0xCC: // DAC
            case 0xDA: // SOS
            case 0xDB: // DQT
            case 0xDC: // DNL
            case 0xDD: // DRI
            case 0xDE: // DHP
            case 0xDF: // EXP
            case 0xE0:
            case 0xE1:
            case 0xE2:
            case 0xE3:
            case 0xE4:
            case 0xE5:
            case 0xE6:
            case 0xE7:
            case 0xE8:
            case 0xE9:
            case 0xEA:
            case 0xEB:
            case 0xEC:
            case 0xED:
            case 0xEE:
            case 0xEF: // APP
            case 0xFE: // COM
                return true;
            default:
                return false;
        }
    }

    private static string GetMarkerName(byte marker) => marker switch
    {
        0xC0 => "SOF0 (Baseline DCT)",
        0xC1 => "SOF1 (Extended sequential)",
        0xC2 => "SOF2 (Progressive)",
        0xC4 => "DHT (Huffman Table)",
        0xDB => "DQT (Quantization Table)",
        0xDD => "DRI (Restart Interval)",
        0xDA => "SOS (Start of Scan)",
        0xD8 => "SOI (Start of Image)",
        0xD9 => "EOI (End of Image)",
        var m when (m >= 0xE0 && m <= 0xEF) => $"APP{m - 0xE0} (Application)",
        0xFE => "COM (Comment)",
        var m when (m >= 0xD0 && m <= 0xD7) => $"RST{m - 0xD0}",
        0x01 => "TEM",
        _ => $"UNKNOWN (0x{marker:X2})"
    };
}
