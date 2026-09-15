using Gateway.Nlp.Abstractions;
using Gateway.Nlp.Cache;
using Gateway.Nlp.Intent;
using Gateway.Nlp.Mql;
using Gateway.Nlp.Slots;

namespace Gateway.Nlp.Router;

public sealed class NlpRouter : INlpRouter
{
    private readonly ITextEmbedder _embedder;
    private readonly ISemanticCache _cache;
    private readonly IMqlBuilder _mqlBuilder;
    private readonly Gazetteer _gazetteer;
    private readonly IntentClassifier _intentClassifier = new();

    public NlpRouter(ITextEmbedder embedder, ISemanticCache cache, IMqlBuilder mqlBuilder)
        : this(embedder, cache, mqlBuilder, Gazetteer.LoadFromDirectory(
            Path.Combine(AppContext.BaseDirectory, "Nlp", "Assets", "Gazetteers")))
    {
    }

    public NlpRouter(ITextEmbedder embedder, ISemanticCache cache, IMqlBuilder mqlBuilder, Gazetteer gazetteer)
    {
        _embedder = embedder;
        _cache = cache;
        _mqlBuilder = mqlBuilder;
        _gazetteer = gazetteer;
    }

    public NlpRouteResult Route(string utterance)
    {
        var normalized = utterance.Trim();
        var vector = _embedder.Embed(normalized);

        var cached = _cache.TryFind(vector);
        if (cached is not null)
        {
            return new NlpRouteResult(
                NlpRouteKind.CacheHit, cached.Mql, null,
                SemanticCacheHit: true, SlotExtractionUsed: false,
                Intent: IntentKind.Search, JustRunIt: false,
                ClarificationsApplied: MqlDefaults.Standard, LlmTokensConsumed: 0);
        }

        var intent = _intentClassifier.Classify(normalized);
        if (intent.Kind == IntentKind.Clarify)
        {
            return new NlpRouteResult(
                NlpRouteKind.ClarifyRequired, null, intent.ClarificationQuestion,
                SemanticCacheHit: false, SlotExtractionUsed: false,
                Intent: intent.Kind, JustRunIt: false,
                ClarificationsApplied: MqlDefaults.Standard, LlmTokensConsumed: 0);
        }

        var slots = SlotExtractor.Extract(normalized, _gazetteer);
        var defaults = MqlDefaults.Standard;

        if (intent.IsComplex)
        {
            return new NlpRouteResult(
                NlpRouteKind.ComplexLlmRequired, null, null,
                SemanticCacheHit: false, SlotExtractionUsed: slots.HasAnyConstraints,
                Intent: intent.Kind, JustRunIt: slots.JustRunIt,
                ClarificationsApplied: defaults, LlmTokensConsumed: 0);
        }

        var mql = _mqlBuilder.Build(slots, defaults);
        _cache.Store(vector, normalized, mql);

        return new NlpRouteResult(
            NlpRouteKind.SimpleMql, mql, null,
            SemanticCacheHit: false, SlotExtractionUsed: slots.HasAnyConstraints,
            Intent: intent.Kind, JustRunIt: slots.JustRunIt,
            ClarificationsApplied: defaults, LlmTokensConsumed: 0);
    }
}
