namespace Gateway.Nlp.Cache;

public sealed class SemanticCache : ISemanticCache
{
    private readonly object _gate = new();
    private readonly List<CacheEntry> _entries = new();
    private readonly double _threshold = CacheDefaults.Threshold;

    public CacheEntry? TryFind(float[] queryVector)
    {
        lock (_gate)
        {
            CacheEntry? best = null;
            var bestScore = _threshold;
            foreach (var entry in _entries)
            {
                var score = CosineSimilarity.Cosine(queryVector, entry.Vector);
                if (score > bestScore)
                {
                    best = entry;
                    bestScore = score;
                }
            }

            return best;
        }
    }

    public void Store(float[] queryVector, string canonicalQuery, string mql)
    {
        lock (_gate)
        {
            CacheEntry? best = null;
            var bestScore = _threshold;
            foreach (var entry in _entries)
            {
                var score = CosineSimilarity.Cosine(queryVector, entry.Vector);
                if (score > bestScore)
                {
                    best = entry;
                    bestScore = score;
                }
            }

            if (best is not null)
            {
                _entries.Remove(best);
            }

            _entries.Add(new CacheEntry(queryVector, mql, canonicalQuery));
        }
    }
}
