namespace Gateway.Nlp.Intent;

public sealed class IntentClassifier
{
    private static readonly string[] ExportSignals =
        ["export", "download", "csv", "xlsx", "send me a file"];

    private static readonly string[] ComplexSignals =
        ["average", "avg", "median", "total", "count of", "compare", "trend", "season",
         "by market", "group", "ranking", "coziest", "most expensive", "cheapest",
         "highest", "lowest", "near "];

    private static readonly string[] ClarifySignals =
        ["what can you", "how do you", "what do you", "help", "hi", "hello", "hey"];

    public IntentResult Classify(string utterance)
    {
        var lower = utterance.ToLowerInvariant();
        var trimmed = lower.Trim();

        var isExport = ExportSignals.Any(lower.Contains);
        var isComplex = ComplexSignals.Any(lower.Contains);
        var isClarifyGreeting = ClarifySignals.Any(lower.Contains) &&
                                !IsListingNounPresent(lower) &&
                                !lower.Contains(" in ");

        if (trimmed is "hi" or "hello" or "hey" or "help")
        {
            return new IntentResult(IntentKind.Clarify, false, "What would you like to do? I can find listings, filter by price or market, or export results.");
        }

        if (isClarifyGreeting)
        {
            return new IntentResult(IntentKind.Clarify, false, "What would you like to do? I can find listings, filter by price or market, or export results.");
        }

        if (isExport)
        {
            return new IntentResult(IntentKind.Export, false, null);
        }

        return new IntentResult(IntentKind.Search, isComplex, null);
    }

    private static bool IsListingNounPresent(string lower)
    {
        return lower.Contains("listing") || lower.Contains("home") || lower.Contains("place") ||
               lower.Contains("apartment") || lower.Contains("property") || lower.Contains("house") ||
               lower.Contains("stay") || lower.Contains("market");
    }
}
