namespace Image.Otp.Core.Enums;

public enum JpegMarker : byte
{
    SOI = 0xD8, // Start of Image
    EOI = 0xD9, // End of Image
    SOF0 = 0xC0, // Baseline DCT
    DHT = 0xC4, // Huffman Table
    DQT = 0xDB, // Quantization Table
    SOS = 0xDA, // Start of Scan
    FF = 0xFF, // Start of segment
}