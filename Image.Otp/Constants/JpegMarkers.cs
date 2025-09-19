using System.Runtime.Intrinsics.X86;
using System.Runtime.Intrinsics;

namespace Image.Otp.Constants;

public static class JpegMarkers
{
    // SOF markers
    public const int SOF0 = 0xC0;
    public const int SOF1 = 0xC1;
    public const int SOF2 = 0xC2;
    public const int SOF3 = 0xC3;
    public const int SOF5 = 0xC5;
    public const int SOF6 = 0xC6;
    public const int SOF7 = 0xC7;
    public const int SOF9 = 0xC9;
    public const int SOF10 = 0xCA;
    public const int SOF11 = 0xCB;
    public const int SOF13 = 0xCD;
    public const int SOF14 = 0xCE;
    public const int SOF15 = 0xCF;

    // Other markers with length data
    public const int DHT = 0xC4;  // Define Huffman Table
    public const int DAC = 0xCC;  // Define Arithmetic Coding
    public const int SOS = 0xDA;  // Start of Scan
    public const int DQT = 0xDB;  // Define Quantization Table
    public const int DNL = 0xDC;  // Define Number of Lines
    public const int DRI = 0xDD;  // Define Restart Interval
    public const int DHP = 0xDE;  // Define Hierarchical Progression
    public const int EXP = 0xDF;  // Expand Reference Component

    // APP markers (Application-specific)
    public const int APP0 = 0xE0;
    public const int APP1 = 0xE1;
    public const int APP2 = 0xE2;
    public const int APP3 = 0xE3;
    public const int APP4 = 0xE4;
    public const int APP5 = 0xE5;
    public const int APP6 = 0xE6;
    public const int APP7 = 0xE7;
    public const int APP8 = 0xE8;
    public const int APP9 = 0xE9;
    public const int APP10 = 0xEA;
    public const int APP11 = 0xEB;
    public const int APP12 = 0xEC;
    public const int APP13 = 0xED;
    public const int APP14 = 0xEE;
    public const int APP15 = 0xEF;

    // Comment marker
    public const int COM = 0xFE;  // Comment


    public const int EOI = 0xD9;

    //
    public const byte FF = 0xFF;
    public const int OO = 0x00;
    public const int D0 = 0xD0;
    public const int D7 = 0xD7;

    //private static readonly HashSet<int> MarkerSet = new()
    //{
    //    SOF0, SOF1, SOF2, SOF3, SOF5, SOF6, SOF7, SOF9,
    //    SOF10, SOF11, SOF13, SOF14, SOF15, DHT, DAC, SOS,
    //    DQT, DNL, DRI, DHP, EXP, APP0, APP1, APP2, APP3,
    //    APP4, APP5, APP6, APP7, APP8, APP9, APP10, APP11,
    //    APP12, APP13, APP14, APP15, COM
    //};
    //public static bool HasLengthData(int marker) => MarkerSet.Contains(marker);

    //public static bool HasLengthData(int marker)
    //{
    //    if (Avx2.IsSupported)
    //    {
    //        var markers = Vector256.Create(SOF0, SOF1, SOF2, SOF3, SOF5, SOF6, SOF7, SOF9);
    //        var markers2 = Vector256.Create(SOF10, SOF11, SOF13, SOF14, SOF15, DHT, DAC, SOS);
    //        var markers3 = Vector256.Create(DQT, DNL, DRI, DHP, EXP, APP0, APP1, APP2);
    //        var markers4 = Vector256.Create(APP3, APP4, APP5, APP6, APP7, APP8, APP9, APP10);
    //        var markers5 = Vector256.Create(APP11, APP12, APP13, APP14, APP15, COM, 0, 0);

    //        var search = Vector256.Create(marker);

    //        return Avx2.MoveMask(Avx2.CompareEqual(markers, search).AsByte()) != 0 ||
    //               Avx2.MoveMask(Avx2.CompareEqual(markers2, search).AsByte()) != 0 ||
    //               Avx2.MoveMask(Avx2.CompareEqual(markers3, search).AsByte()) != 0 ||
    //               Avx2.MoveMask(Avx2.CompareEqual(markers4, search).AsByte()) != 0 ||
    //               Avx2.MoveMask(Avx2.CompareEqual(markers5, search).AsByte()) != 0;
    //    }

    //    if (Sse2.IsSupported)
    //    {
    //        Vector128<int> search = Vector128.Create(marker);

    //        return Sse2.MoveMask(Sse2.CompareEqual(Vector128.Create(SOF0, SOF1, SOF2, SOF3), search).AsByte()) != 0 ||
    //               Sse2.MoveMask(Sse2.CompareEqual(Vector128.Create(SOF5, SOF6, SOF7, SOF9), search).AsByte()) != 0 ||
    //               Sse2.MoveMask(Sse2.CompareEqual(Vector128.Create(SOF10, SOF11, SOF13, SOF14), search).AsByte()) != 0 ||
    //               Sse2.MoveMask(Sse2.CompareEqual(Vector128.Create(SOF15, DHT, DAC, SOS), search).AsByte()) != 0 ||
    //               Sse2.MoveMask(Sse2.CompareEqual(Vector128.Create(DQT, DNL, DRI, DHP), search).AsByte()) != 0 ||
    //               Sse2.MoveMask(Sse2.CompareEqual(Vector128.Create(EXP, APP0, APP1, APP2), search).AsByte()) != 0 ||
    //               Sse2.MoveMask(Sse2.CompareEqual(Vector128.Create(APP3, APP4, APP5, APP6), search).AsByte()) != 0 ||
    //               Sse2.MoveMask(Sse2.CompareEqual(Vector128.Create(APP7, APP8, APP9, APP10), search).AsByte()) != 0 ||
    //               Sse2.MoveMask(Sse2.CompareEqual(Vector128.Create(APP11, APP12, APP13, APP14), search).AsByte()) != 0 ||
    //               Sse2.MoveMask(Sse2.CompareEqual(Vector128.Create(APP15, COM, 0, 0), search).AsByte()) != 0;
    //    }

    //    return marker switch
    //    {
    //        SOF0 or SOF1 or SOF2 or SOF3 or SOF5 or SOF6 or SOF7 or SOF9 or SOF10 or SOF11 or SOF13 or
    //        SOF14 or SOF15 or DHT or DAC or SOS or DQT or DNL or DRI or DHP or EXP or APP0 or APP1 or
    //        APP2 or APP3 or APP4 or APP5 or APP6 or APP7 or APP8 or APP9 or APP10 or APP11 or APP12 or
    //        APP13 or APP14 or APP15 or COM => true,
    //        _ => false,
    //    };
    //}

    //public static bool HasLengthData(int marker)
    //{
    //    ReadOnlySpan<int> markers =
    //    [
    //        SOF0, SOF1, SOF2, SOF3, SOF5, SOF6, SOF7, SOF9,
    //        SOF10, SOF11, SOF13, SOF14, SOF15, DHT, DAC, SOS,
    //        DQT, DNL, DRI, DHP, EXP, APP0, APP1, APP2, APP3,
    //        APP4, APP5, APP6, APP7, APP8, APP9, APP10, APP11,
    //        APP12, APP13, APP14, APP15, COM
    //    ];

    //    return markers.Contains(marker);
    //}

    public static bool HasLengthData(int marker)
    {
        return marker switch
        {
            SOF0 or SOF1 or SOF2 or SOF3 or SOF5 or SOF6 or SOF7 or SOF9 or SOF10 or SOF11 or SOF13 or
            SOF14 or SOF15 or DHT or DAC or SOS or DQT or DNL or DRI or DHP or EXP or APP0 or APP1 or
            APP2 or APP3 or APP4 or APP5 or APP6 or APP7 or APP8 or APP9 or APP10 or APP11 or APP12 or
            APP13 or APP14 or APP15 or COM => true,
            _ => false,
        };
    }

}