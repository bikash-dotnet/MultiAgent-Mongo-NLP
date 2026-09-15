namespace Gateway.Nlp.Llm;

public interface ILlmQueryGenerator
{
    Task<LlmQueryResult> GenerateAsync(
        string utterance,
        string slotsJson,
        string? previousError,
        CancellationToken cancellationToken);
}
