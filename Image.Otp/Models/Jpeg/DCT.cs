namespace Image.Otp.Models.Jpeg;

public class DCT
{
    private const int N = 8; // Block size for DCT

    public static double[,] ForwardDCT(double[,] block)
    {
        double[,] dct = new double[N, N];
        for (int u = 0; u < N; u++)
        {
            for (int v = 0; v < N; v++)
            {
                double sum = 0.0;
                for (int x = 0; x < N; x++)
                {
                    for (int y = 0; y < N; y++)
                    {
                        sum += block[x, y] * Math.Cos((2 * x + 1) * u * Math.PI / (2 * N)) * Math.Cos((2 * y + 1) * v * Math.PI / (2 * N));
                    }
                }
                double cu = (u == 0) ? 1 / Math.Sqrt(2) : 1;
                double cv = (v == 0) ? 1 / Math.Sqrt(2) : 1;
                dct[u, v] = 0.25 * cu * cv * sum;
            }
        }
        return dct;
    }

    public static double[,] InverseDCT(double[,] dct)
    {
        double[,] block = new double[N, N];
        for (int x = 0; x < N; x++)
        {
            for (int y = 0; y < N; y++)
            {
                double sum = 0.0;
                for (int u = 0; u < N; u++)
                {
                    for (int v = 0; v < N; v++)
                    {
                        double cu = (u == 0) ? 1 / Math.Sqrt(2) : 1;
                        double cv = (v == 0) ? 1 / Math.Sqrt(2) : 1;
                        sum += cu * cv * dct[u, v] * Math.Cos((2 * x + 1) * u * Math.PI / (2 * N)) * Math.Cos((2 * y + 1) * v * Math.PI / (2 * N));
                    }
                }
                block[x, y] = 0.25 * sum;
            }
        }
        return block;
    }
}
