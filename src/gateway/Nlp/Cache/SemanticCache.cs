using Gateway.Configuration;
using System.Text.Json;

namespace Gateway.Nlp.Cache;

public sealed class SemanticCache : ISemanticCache
{
    private readonly object _gate = new();
    private readonly List<CacheEntry> _entries = new();
    private readonly ISettingsProvider _provider;

    public SemanticCache(ISettingsProvider provider)
    {
        _provider = provider;
    }

    public CacheEntry? TryFind(float[] queryVector)
    {
        lock (_gate)
        {
            CacheEntry? best = null;
            var bestScore = GetThreshold();
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
            var bestScore = GetThreshold();
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

    private double GetThreshold()
    {
        if (_provider.TryGetValue("Nlp.Cache", "Threshold", out var value) &&
            value.ValueKind == JsonValueKind.Number)
        {
            return value.GetDouble();
        }
        return CacheDefaults.Threshold;
    }
}
