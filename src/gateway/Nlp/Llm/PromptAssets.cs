using System.Text;
using Gateway.Schemas;

namespace Gateway.Nlp.Llm;

public sealed class PromptAssets
{
    private const string ErrorHeader =
        "\nThe previous attempt was rejected. Fix this problem and return ONLY valid JSON:\n";

    private readonly string _fallbackSchema;
    private readonly ActiveSchemaProvider? _provider;

    public string PromptTemplate { get; }
    public string Sample { get; }
    public string Examples { get; }

    public string Schema => RenderSchema();

    private PromptAssets(
        string promptTemplate,
        string fallbackSchema,
        string sample,
        string examples,
        ActiveSchemaProvider? provider)
    {
        PromptTemplate = promptTemplate;
        _fallbackSchema = fallbackSchema;
        Sample = sample;
        Examples = examples;
        _provider = provider;
    }

    public static PromptAssets LoadFromDirectory(string directory, ActiveSchemaProvider? provider = null)
    {
        return new PromptAssets(
            File.ReadAllText(Path.Combine(directory, "prompt_template.txt")),
            File.ReadAllText(Path.Combine(directory, "schema.txt")),
            File.ReadAllText(Path.Combine(directory, "sample.txt")),
            File.ReadAllText(Path.Combine(directory, "examples.txt")),
            provider);
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

    private string RenderSchema()
    {
        var schema = _provider?.ActiveSchema;
        if (schema is null || schema.Fields.Count == 0)
        {
            return _fallbackSchema;
        }

        var builder = new StringBuilder();
        builder.Append("collection: ").Append(schema.Database).Append('.').AppendLine(schema.Collection);
        builder.AppendLine("fields:");

        foreach (var field in schema.Fields)
        {
            builder.Append("- ").Append(field.Path).Append(": ").Append(ToTypeName(field.DataType));

            var annotations = new List<string>();
            if (field.IsSensitive)
            {
                annotations.Add("sensitive");
            }

            if (field.RequiresApproval)
            {
                annotations.Add("requires approval");
            }

            if (annotations.Count > 0)
            {
                builder.Append(" [").Append(string.Join(", ", annotations)).Append(']');
            }

            if (!string.IsNullOrWhiteSpace(field.Description))
            {
                builder.Append(" (").Append(field.Description).Append(')');
            }

            builder.AppendLine();
        }

        return builder.ToString().TrimEnd();
    }

    private static string ToTypeName(SchemaFieldType dataType)
    {
        return dataType switch
        {
            SchemaFieldType.Array => "array",
            SchemaFieldType.Object => "object",
            _ => dataType.ToString().ToLowerInvariant()
        };
    }
}
