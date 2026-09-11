namespace Gateway.Nlp.Intent;

public enum IntentKind
{
    Search,
    Export,
    Clarify
}

public sealed record IntentResult(IntentKind Kind, bool IsComplex, string? ClarificationQuestion);
