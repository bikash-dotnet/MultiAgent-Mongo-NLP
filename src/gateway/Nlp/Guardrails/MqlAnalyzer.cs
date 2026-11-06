using System.Text.Json;

namespace Gateway.Nlp.Guardrails;

public static class MqlAnalyzer
{
    public static MqlAnalysis Analyze(string? pipelineJson)
    {
        if (string.IsNullOrWhiteSpace(pipelineJson))
        {
            return Invalid("pipeline is empty");
        }

        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(pipelineJson);
        }
        catch (JsonException ex)
        {
            return Invalid($"invalid json: {ex.Message}");
        }

        using (document)
        {
            if (document.RootElement.ValueKind != JsonValueKind.Array)
            {
                return Invalid("pipeline must be a JSON array");
            }

            var fields = new FieldCollector();
            var operators = new OperatorCollector();

            foreach (var stage in document.RootElement.EnumerateArray())
            {
                if (stage.ValueKind != JsonValueKind.Object)
                {
                    return Invalid("each stage must be an object");
                }

                foreach (var stageProperty in stage.EnumerateObject())
                {
                    operators.Add(stageProperty.Name);
                    CollectStage(stageProperty.Name, stageProperty.Value, fields, operators);
                }
            }

            return new MqlAnalysis(true, null, fields.ToList(), operators.ToList());
        }
    }

    public static IReadOnlyDictionary<string, IReadOnlyList<string>> ExtractEqualityValues(string? pipelineJson)
    {
        var values = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrWhiteSpace(pipelineJson))
        {
            return ToReadOnly(values);
        }

        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(pipelineJson);
        }
        catch (JsonException)
        {
            return ToReadOnly(values);
        }

        using (document)
        {
            if (document.RootElement.ValueKind != JsonValueKind.Array)
            {
                return ToReadOnly(values);
            }

            foreach (var stage in document.RootElement.EnumerateArray())
            {
                if (stage.ValueKind != JsonValueKind.Object)
                {
                    continue;
                }

                foreach (var stageProperty in stage.EnumerateObject())
                {
                    if (stageProperty.Name == "$match")
                    {
                        CollectEqualityValues(stageProperty.Value, values);
                    }
                }
            }
        }

        return ToReadOnly(values);
    }

    private static MqlAnalysis Invalid(string reason)
    {
        return new MqlAnalysis(false, reason, [], []);
    }

    private static void CollectEqualityValues(JsonElement filter, Dictionary<string, List<string>> values)
    {
        if (filter.ValueKind != JsonValueKind.Object)
        {
            return;
        }

        foreach (var property in filter.EnumerateObject())
        {
            if (property.Name is "$and" or "$or")
            {
                if (property.Value.ValueKind == JsonValueKind.Array)
                {
                    foreach (var item in property.Value.EnumerateArray())
                    {
                        CollectEqualityValues(item, values);
                    }
                }

                continue;
            }

            if (property.Name.StartsWith('$'))
            {
                continue;
            }

            AddLiteral(values, property.Name, property.Value);
            if (property.Value.ValueKind == JsonValueKind.Object)
            {
                foreach (var inner in property.Value.EnumerateObject())
                {
                    if (inner.Name is "$eq" or "$in")
                    {
                        AddLiteral(values, property.Name, inner.Value);
                    }
                }
            }
        }
    }

    private static void AddLiteral(Dictionary<string, List<string>> values, string field, JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in element.EnumerateArray())
            {
                AddLiteral(values, field, item);
            }

            return;
        }

        var text = element.ValueKind switch
        {
            JsonValueKind.String => element.GetString(),
            JsonValueKind.Number => element.GetRawText(),
            JsonValueKind.True => "true",
            JsonValueKind.False => "false",
            _ => null
        };

        if (string.IsNullOrWhiteSpace(field) || text is null)
        {
            return;
        }

        if (!values.TryGetValue(field, out var list))
        {
            list = [];
            values[field] = list;
        }

        if (!list.Contains(text, StringComparer.OrdinalIgnoreCase))
        {
            list.Add(text);
        }
    }

    private static IReadOnlyDictionary<string, IReadOnlyList<string>> ToReadOnly(
        Dictionary<string, List<string>> values) =>
        values.ToDictionary(
            pair => pair.Key,
            pair => (IReadOnlyList<string>)pair.Value,
            StringComparer.OrdinalIgnoreCase);

    private static void CollectStage(
        string stage,
        JsonElement value,
        FieldCollector fields,
        OperatorCollector operators)
    {
        switch (stage)
        {
            case "$match":
                CollectFilter(value, fields, operators);
                break;
            case "$sort":
                CollectSort(value, fields);
                break;
            case "$project":
            case "$addFields":
            case "$set":
                CollectProjection(value, fields, operators);
                break;
            case "$group":
                CollectGroup(value, fields, operators);
                break;
            case "$unwind":
                CollectUnwind(value, fields);
                break;
            default:
                WalkExpression(value, fields, operators);
                break;
        }
    }

    private static void CollectFilter(
        JsonElement filter,
        FieldCollector fields,
        OperatorCollector operators)
    {
        if (filter.ValueKind != JsonValueKind.Object)
        {
            return;
        }

        foreach (var property in filter.EnumerateObject())
        {
            if (property.Name.StartsWith('$'))
            {
                operators.Add(property.Name);
                CollectLogical(property.Value, fields, operators);
                continue;
            }

            fields.Add(property.Name);

            if (property.Value.ValueKind == JsonValueKind.Object)
            {
                var hasOperator = property.Value.EnumerateObject().Any(p => p.Name.StartsWith('$'));
                if (hasOperator)
                {
                    WalkExpression(property.Value, fields, operators);
                }
                else
                {
                    CollectNestedDocument(property.Name, property.Value, fields, operators);
                }
            }
            else
            {
                WalkExpression(property.Value, fields, operators);
            }
        }
    }

    private static void CollectLogical(
        JsonElement value,
        FieldCollector fields,
        OperatorCollector operators)
    {
        if (value.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in value.EnumerateArray())
            {
                CollectFilter(item, fields, operators);
            }
        }
        else if (value.ValueKind == JsonValueKind.Object)
        {
            CollectFilter(value, fields, operators);
        }
        else
        {
            WalkExpression(value, fields, operators);
        }
    }

    private static void CollectNestedDocument(
        string prefix,
        JsonElement document,
        FieldCollector fields,
        OperatorCollector operators)
    {
        foreach (var property in document.EnumerateObject())
        {
            if (property.Name.StartsWith('$'))
            {
                operators.Add(property.Name);
                WalkExpression(property.Value, fields, operators);
                continue;
            }

            var path = prefix + "." + property.Name;
            fields.Add(path);

            if (property.Value.ValueKind == JsonValueKind.Object)
            {
                var hasOperator = property.Value.EnumerateObject().Any(p => p.Name.StartsWith('$'));
                if (hasOperator)
                {
                    WalkExpression(property.Value, fields, operators);
                }
                else
                {
                    CollectNestedDocument(path, property.Value, fields, operators);
                }
            }
        }
    }

    private static void CollectSort(JsonElement value, FieldCollector fields)
    {
        if (value.ValueKind != JsonValueKind.Object)
        {
            return;
        }

        foreach (var property in value.EnumerateObject())
        {
            if (!property.Name.StartsWith('$'))
            {
                fields.Add(property.Name);
            }
        }
    }

    private static void CollectProjection(
        JsonElement value,
        FieldCollector fields,
        OperatorCollector operators)
    {
        if (value.ValueKind != JsonValueKind.Object)
        {
            return;
        }

        foreach (var property in value.EnumerateObject())
        {
            if (property.Name.StartsWith('$'))
            {
                operators.Add(property.Name);
                WalkExpression(property.Value, fields, operators);
                continue;
            }

            fields.Add(property.Name);

            if (property.Value.ValueKind == JsonValueKind.String &&
                property.Value.GetString() is { } reference &&
                reference.StartsWith('$'))
            {
                AddReference(reference, fields);
            }
            else
            {
                WalkExpression(property.Value, fields, operators);
            }
        }
    }

    private static void CollectGroup(
        JsonElement value,
        FieldCollector fields,
        OperatorCollector operators)
    {
        if (value.ValueKind != JsonValueKind.Object)
        {
            return;
        }

        foreach (var property in value.EnumerateObject())
        {
            if (property.Name == "_id")
            {
                CollectGroupKey(property.Value, fields, operators);
                continue;
            }

            if (property.Value.ValueKind == JsonValueKind.Object)
            {
                foreach (var accumulator in property.Value.EnumerateObject())
                {
                    operators.Add(accumulator.Name);
                    WalkExpression(accumulator.Value, fields, operators);
                }
            }
        }
    }

    private static void CollectGroupKey(
        JsonElement value,
        FieldCollector fields,
        OperatorCollector operators)
    {
        if (value.ValueKind == JsonValueKind.String)
        {
            AddReference(value.GetString(), fields);
        }
        else if (value.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in value.EnumerateObject())
            {
                if (property.Name.StartsWith('$'))
                {
                    operators.Add(property.Name);
                    WalkExpression(property.Value, fields, operators);
                }
                else
                {
                    fields.Add(property.Name);
                    WalkExpression(property.Value, fields, operators);
                }
            }
        }
    }

    private static void CollectUnwind(JsonElement value, FieldCollector fields)
    {
        if (value.ValueKind == JsonValueKind.String)
        {
            AddReference(value.GetString(), fields);
        }
        else if (value.ValueKind == JsonValueKind.Object &&
                 value.TryGetProperty("path", out var path) &&
                 path.ValueKind == JsonValueKind.String)
        {
            AddReference(path.GetString(), fields);
        }
    }

    private static void WalkExpression(
        JsonElement value,
        FieldCollector fields,
        OperatorCollector operators)
    {
        switch (value.ValueKind)
        {
            case JsonValueKind.String:
                AddReference(value.GetString(), fields);
                break;
            case JsonValueKind.Object:
                foreach (var property in value.EnumerateObject())
                {
                    if (property.Name.StartsWith('$'))
                    {
                        operators.Add(property.Name);
                    }

                    WalkExpression(property.Value, fields, operators);
                }

                break;
            case JsonValueKind.Array:
                foreach (var item in value.EnumerateArray())
                {
                    WalkExpression(item, fields, operators);
                }

                break;
        }
    }

    private static void AddReference(string? value, FieldCollector fields)
    {
        if (string.IsNullOrWhiteSpace(value) || value[0] != '$')
        {
            return;
        }

        if (value.StartsWith("$$", StringComparison.Ordinal))
        {
            return;
        }

        fields.Add(value[1..]);
    }

    private sealed class FieldCollector
    {
        private readonly HashSet<string> _seen = new(StringComparer.Ordinal);
        private readonly List<string> _ordered = [];

        public void Add(string fieldPath)
        {
            if (!string.IsNullOrWhiteSpace(fieldPath) && _seen.Add(fieldPath))
            {
                _ordered.Add(fieldPath);
            }
        }

        public IReadOnlyList<string> ToList() => _ordered;
    }

    private sealed class OperatorCollector
    {
        private readonly HashSet<string> _seen = new(StringComparer.Ordinal);
        private readonly List<string> _ordered = [];

        public void Add(string name)
        {
            if (name.StartsWith('$') && _seen.Add(name))
            {
                _ordered.Add(name);
            }
        }

        public IReadOnlyList<string> ToList() => _ordered;
    }
}
