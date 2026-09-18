namespace Gateway.Nlp.Guardrails;

public interface ISensitiveFieldRegistry
{
    IReadOnlyList<SensitiveFieldFlag> All { get; }

    bool TryGet(string fieldPath, out SensitiveFieldFlag flag);

    IReadOnlyList<SensitiveFieldFlag> Match(IEnumerable<string> fieldPaths);
}
