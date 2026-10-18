using System.Text.Json;
using Gateway.Nlp.Abstractions;
using Gateway.Nlp.Cache;
using Gateway.Configuration;
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
    private readonly ISettingsProvider _settings;
    private readonly IntentClassifier _intentClassifier = new();

    public NlpRouter(ITextEmbedder embedder, ISemanticCache cache, IMqlBuilder mqlBuilder)
        : this(embedder, cache, mqlBuilder, Gazetteer.LoadFromDirectory(
            Path.Combine(AppContext.BaseDirectory, "Nlp", "Assets", "Gazetteers")))
    {
    }

    public NlpRouter(ITextEmbedder embedder, ISemanticCache cache, IMqlBuilder mqlBuilder, Gazetteer gazetteer)
        : this(embedder, cache, mqlBuilder, gazetteer, new SettingsProvider())
    {
    }

    public NlpRouter(ITextEmbedder embedder, ISemanticCache cache, IMqlBuilder mqlBuilder, Gazetteer gazetteer, ISettingsProvider settings)
    {
        _embedder = embedder;
        _cache = cache;
        _mqlBuilder = mqlBuilder;
        _gazetteer = gazetteer;
        _settings = settings;
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
                ClarificationsApplied: GetDefaults(), LlmTokensConsumed: 0);
        }

        var intent = _intentClassifier.Classify(normalized);
        if (intent.Kind == IntentKind.Clarify)
        {
            return new NlpRouteResult(
                NlpRouteKind.ClarifyRequired, null, intent.ClarificationQuestion,
                SemanticCacheHit: false, SlotExtractionUsed: false,
                Intent: intent.Kind, JustRunIt: false,
                ClarificationsApplied: GetDefaults(), LlmTokensConsumed: 0);
        }

        var slots = SlotExtractor.Extract(normalized, _gazetteer);
        var defaults = GetDefaults();

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

    private MqlDefaults GetDefaults()
    {
        int limit = MqlDefaults.Standard.Limit;
        string sort = MqlDefaults.Standard.Sort;
        string market = MqlDefaults.Standard.Market;

        if (_settings.TryGetValue("Nlp.Defaults", "Limit", out var limitValue) &&
            limitValue.ValueKind == JsonValueKind.Number)
        {
            limit = limitValue.GetInt32();
        }

        if (_settings.TryGetValue("Nlp.Defaults", "Sort", out var sortValue) &&
            sortValue.ValueKind == JsonValueKind.String)
        {
            sort = sortValue.GetString() ?? MqlDefaults.Standard.Sort;
        }

        if (_settings.TryGetValue("Nlp.Defaults", "Market", out var marketValue) &&
            marketValue.ValueKind == JsonValueKind.String)
        {
            market = marketValue.GetString() ?? MqlDefaults.Standard.Market;
        }

        return new MqlDefaults(limit, sort, market);
    }
}
