namespace Gateway.Nlp.Cache;

public sealed record CacheEntry(float[] Vector, string Mql, string CanonicalQuery);

public interface ISemanticCache
{
    CacheEntry? TryFind(float[] queryVector);
    void Store(float[] queryVector, string canonicalQuery, string mql);
}
