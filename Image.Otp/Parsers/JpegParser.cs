using Image.Otp.Models.Jpeg;

namespace Image.Otp.Parsers;

public sealed class JpegParser
{
    public static List<JpegSegment> ParseSegments(byte[] bytes)
    {
        ArgumentNullException.ThrowIfNull(bytes);
        if (bytes.Length < 2) throw new ArgumentException("Input too small to be a JPEG");

        var segments = new List<JpegSegment>();

        // Check SOI at beginning
        if (bytes[0] != 0xFF || bytes[1] != 0xD8)
            throw new InvalidDataException("Missing JPEG SOI marker (0xFFD8) at start.");

        // Add SOI segment
        segments.Add(new JpegSegment
        {
            Offset = 0,
            Marker = 0xD8,
            Name = GetMarkerName(0xD8),
            Length = 0,
            Data = []
        });

        var pos = 2;
        var inEntropy = false;
        var entropyStart = -1;

        while (pos < bytes.Length)
        {
            if (!inEntropy)
            {
                // Find next 0xFF byte (start of a marker)
                int ffPos = pos;
                while (ffPos < bytes.Length && bytes[ffPos] != 0xFF) ffPos++;
                if (ffPos >= bytes.Length) break;

                // Skip any padding 0xFF bytes to get the marker byte
                int markerPos = ffPos;
                while (markerPos < bytes.Length && bytes[markerPos] == 0xFF) markerPos++;
                if (markerPos >= bytes.Length) break;

                byte marker = bytes[markerPos];
                pos = markerPos + 1; // position after marker byte
                int markerStartOffset = ffPos;

                // Stuffed 0xFF (0xFF00) may appear — if the byte after 0xFF is 0x00, that's not a marker.
                if (marker == 0x00)
                {
                    // Strange to see before SOS, but we'll treat as data and continue scanning after it.
                    pos = markerPos + 1;
                    continue;
                }

                // Markers that do NOT have a length field:
                //  - SOI (D8), EOI (D9), RST0..RST7 (D0..D7), TEM (01)
                bool hasLength = !(
                    marker == 0xD8 || marker == 0xD9 ||
                    (marker >= 0xD0 && marker <= 0xD7) ||
                    marker == 0x01
                );

                if (!hasLength)
                {
                    // Marker-only segment
                    segments.Add(new JpegSegment
                    {
                        Offset = markerStartOffset,
                        Marker = marker,
                        Name = GetMarkerName(marker),
                        Length = 0,
                        Data = Array.Empty<byte>()
                    });
                    // If EOI, we're done
                    if (marker == 0xD9) break;
                }
                else
                {
                    // Read 2-byte big-endian length
                    if (pos + 1 >= bytes.Length) throw new InvalidDataException("Truncated JPEG segment length.");
                    int len = (bytes[pos] << 8) | bytes[pos + 1];
                    pos += 2;

                    if (len < 2) throw new InvalidDataException($"Invalid segment length {len} for marker 0xFF{marker:X2}.");

                    int payloadLen = len - 2;
                    if (pos + payloadLen > bytes.Length) throw new InvalidDataException("Truncated JPEG segment payload.");

                    byte[] payload = new byte[payloadLen];
                    Array.Copy(bytes, pos, payload, 0, payloadLen);

                    segments.Add(new JpegSegment
                    {
                        Offset = markerStartOffset,
                        Marker = marker,
                        Name = GetMarkerName(marker),
                        Length = len,
                        Data = payload
                    });

                    pos += payloadLen;

                    // If this is Start Of Scan, the entropy-coded data starts immediately after the SOS payload.
                    if (marker == 0xDA)
                    {
                        inEntropy = true;
                        entropyStart = pos;
                    }
                }
            }
            else
            {
                // We are in entropy-coded data (after SOS). Scan until the next marker (0xFF followed by non-zero).
                var k = pos;
                while (k < bytes.Length)
                {
                    if (bytes[k] == 0xFF)
                    {
                        // need to look at next byte to decide whether this is:
                        //  - stuffed 0x00 (0xFF00) -> part of data
                        //  - additional 0xFF padding -> skip it
                        //  - a real marker (0xFF followed by non-zero)
                        if (k + 1 >= bytes.Length)
                        {
                            // truncated or ends with 0xFF — treat the rest as data
                            k = bytes.Length;
                            break;
                        }

                        var next = bytes[k + 1];
                        if (next == 0x00)
                        {
                            // stuffed byte -> part of data; skip both
                            k += 2;
                            continue;
                        }
                        if (next == 0xFF)
                        {
                            // padding multiple 0xFF bytes; move forward one and re-evaluate
                            k++;
                            continue;
                        }

                        // Found a marker (0xFF followed by a non-zero byte)
                        break;
                    }
                    else
                    {
                        k++;
                    }
                }

                var compEnd = Math.Min(k, bytes.Length);
                var compLen = compEnd - entropyStart;
                var compData = new byte[compLen];
                if (compLen > 0) Array.Copy(bytes, entropyStart, compData, 0, compLen);

                segments.Add(new JpegSegment
                {
                    Offset = entropyStart,
                    Marker = 0x00, // marker==0 indicates compressed-data segment (not an actual 0xFFxx marker)
                    Name = "CompressedData",
                    Length = compLen,
                    Data = compData
                });

                // Move pos to the 0xFF that starts the marker (or EOF)
                pos = k;
                inEntropy = false; // return to marker parsing loop to handle the marker we found (if any)
            }
        }

        return segments;
    }

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

    public static List<JpegSegment> ParseSegmentsV2(byte[] bytes)
    {
        List<JpegSegment> segments = new List<JpegSegment>();
        int i = 0;

        while (i < bytes.Length)
        {
            if (bytes[i] != 0xFF)
            {
                i++;
                continue;
            }

            int markerOffset = i;
            i++;

            while (i < bytes.Length && bytes[i] == 0xFF)
                i++;

            if (i >= bytes.Length)
                break;

            byte markerByte = bytes[i];
            if (markerByte == 0x00)
            {
                i++;
                continue;
            }

            i++;

            bool hasLength = !((markerByte >= 0xD0 && markerByte <= 0xD7) || markerByte == 0xD8 || markerByte == 0xD9 || markerByte == 0x01);
            int length = 0;
            int dataStart = i;
            int dataLength = 0;

            if (hasLength)
            {
                if (i + 1 >= bytes.Length)
                    break;

                length = (bytes[i] << 8) | bytes[i + 1];
                dataStart = i + 2;
                dataLength = length - 2;

                if (dataStart + dataLength > bytes.Length)
                    break;
            }

            if (markerByte == 0xDA)
            {
                byte[] data = new byte[dataLength];
                Array.Copy(bytes, dataStart, data, 0, dataLength);
                segments.Add(new JpegSegment
                {
                    Offset = markerOffset,
                    Marker = markerByte,
                    Name = GetMarkerName(markerByte),
                    Length = length,
                    Data = data
                });

                int compressedStart = dataStart + dataLength;
                while (compressedStart < bytes.Length)
                {
                    if (bytes[compressedStart] == 0xFF)
                    {
                        int j = compressedStart + 1;
                        while (j < bytes.Length && bytes[j] == 0xFF)
                            j++;

                        if (j >= bytes.Length)
                            break;

                        if (bytes[j] == 0x00)
                        {
                            compressedStart = j + 1;
                            continue;
                        }

                        i = compressedStart;
                        break;
                    }
                    compressedStart++;
                }

                if (compressedStart >= bytes.Length)
                    break;
            }
            else
            {
                byte[] data = hasLength ? new byte[dataLength] : Array.Empty<byte>();
                if (hasLength)
                    Array.Copy(bytes, dataStart, data, 0, dataLength);

                segments.Add(new JpegSegment
                {
                    Offset = markerOffset,
                    Marker = markerByte,
                    Name = GetMarkerName(markerByte),
                    Length = length,
                    Data = data
                });

                if (hasLength)
                    i = dataStart + dataLength;
                else
                    i = markerOffset + 2;

                if (markerByte == 0xD9)
                    break;
            }
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
