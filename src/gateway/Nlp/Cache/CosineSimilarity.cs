namespace Gateway.Nlp.Cache;

public static class CacheDefaults
{
    public const double Threshold = 0.95;
}

public static class CosineSimilarity
{
    public static double Cosine(float[] a, float[] b)
    {
        double dot = 0, normA = 0, normB = 0;
        for (var i = 0; i < a.Length; i++)
        {
            dot += (double)a[i] * b[i];
            normA += (double)a[i] * a[i];
            normB += (double)b[i] * b[i];
        }

        if (normA == 0 || normB == 0) return 0;
        return dot / (Math.Sqrt(normA) * Math.Sqrt(normB));
    }

    public static bool IsAbove(float[] a, float[] b, double threshold)
    {
        return Cosine(a, b) > threshold;
    }
}
