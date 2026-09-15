using System.Text.Json;

namespace Gateway.Nlp.Llm;

public sealed class PipelineValidator : IPipelineValidator
{
    private static readonly HashSet<string> AllowedStages = new(StringComparer.Ordinal)
    {
        "$match", "$sort", "$limit", "$group", "$project", "$unwind", "$addFields", "$count"
    };

    public PipelineValidationResult Validate(string pipeline)
    {
        if (string.IsNullOrWhiteSpace(pipeline))
        {
            return new PipelineValidationResult(false, "pipeline is empty");
        }

        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(pipeline);
        }
        catch (JsonException ex)
        {
            return new PipelineValidationResult(false, $"invalid json: {ex.Message}");
        }

        using (document)
        {
            if (document.RootElement.ValueKind != JsonValueKind.Array)
            {
                return new PipelineValidationResult(false, "pipeline must be a JSON array");
            }

            foreach (var stage in document.RootElement.EnumerateArray())
            {
                if (stage.ValueKind != JsonValueKind.Object)
                {
                    return new PipelineValidationResult(false, "each stage must be an object");
                }

                var properties = stage.EnumerateObject().ToList();
                if (properties.Count != 1 || !AllowedStages.Contains(properties[0].Name))
                {
                    return new PipelineValidationResult(false, "stage must have exactly one allowed operator");
                }

                var stageResult = ValidateStage(properties[0]);
                if (!stageResult.IsValid)
                {
                    return stageResult;
                }
            }
        }

        return new PipelineValidationResult(true, null);
    }

    private static PipelineValidationResult ValidateStage(JsonProperty stage)
    {
        if (stage.Name != "$match" || stage.Value.ValueKind != JsonValueKind.Object)
        {
            return new PipelineValidationResult(true, null);
        }

        if (stage.Value.TryGetProperty("price", out var price))
        {
            var numeric = price.ValueKind == JsonValueKind.Number ||
                          (price.ValueKind == JsonValueKind.Object &&
                           price.EnumerateObject().All(p => p.Value.ValueKind == JsonValueKind.Number));
            if (!numeric)
            {
                return new PipelineValidationResult(false, "price must be numeric");
            }
        }

        if (stage.Value.TryGetProperty("amenities", out var amenities))
        {
            var isArrayExpression = amenities.ValueKind == JsonValueKind.Object &&
                                    (amenities.TryGetProperty("$all", out var all) || amenities.TryGetProperty("$in", out all)) &&
                                    all.ValueKind == JsonValueKind.Array;
            if (!isArrayExpression)
            {
                return new PipelineValidationResult(false, "amenities must use $all or $in with an array");
            }
        }

        return new PipelineValidationResult(true, null);
    }
}
