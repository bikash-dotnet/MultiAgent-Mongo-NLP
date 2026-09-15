namespace Gateway.Nlp.Llm;

public sealed class PromptAssets
{
    private const string ErrorHeader =
        "\nThe previous attempt was rejected. Fix this problem and return ONLY valid JSON:\n";

    public string PromptTemplate { get; }
    public string Schema { get; }
    public string Sample { get; }
    public string Examples { get; }

    private PromptAssets(string promptTemplate, string schema, string sample, string examples)
    {
        PromptTemplate = promptTemplate;
        Schema = schema;
        Sample = sample;
        Examples = examples;
    }

    public static PromptAssets LoadFromDirectory(string directory)
    {
        return new PromptAssets(
            File.ReadAllText(Path.Combine(directory, "prompt_template.txt")),
            File.ReadAllText(Path.Combine(directory, "schema.txt")),
            File.ReadAllText(Path.Combine(directory, "sample.txt")),
            File.ReadAllText(Path.Combine(directory, "examples.txt")));
    }

    public string BuildPrompt(string utterance, string slotsJson, string? previousError)
    {
        var errorBlock = string.IsNullOrWhiteSpace(previousError)
            ? string.Empty
            : ErrorHeader + previousError;

        return PromptTemplate
            .Replace("{{schema}}", Schema)
            .Replace("{{examples}}", Examples.TrimEnd() + Environment.NewLine + Sample.TrimEnd())
            .Replace("{{slots}}", slotsJson)
            .Replace("{{utterance}}", utterance)
            .Replace("{{previous_error}}", errorBlock);
    }
}
