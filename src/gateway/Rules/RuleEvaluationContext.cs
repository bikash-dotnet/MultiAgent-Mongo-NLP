namespace Gateway.Rules;

public sealed class RuleEvaluationContext
{
    public static RuleEvaluationContext Empty { get; } = new();

    public IReadOnlyDictionary<string, IReadOnlyList<string>> FieldValues { get; init; } =
        new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase);

    public IReadOnlySet<string> Fields { get; init; } =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase);

    public static RuleEvaluationContext From(
        IEnumerable<string>? fields = null,
        IEnumerable<KeyValuePair<string, string>>? values = null,
        IReadOnlyDictionary<string, IReadOnlyList<string>>? multiValues = null)
    {
        var fieldSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var valueMap = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);

        if (multiValues is not null)
        {
            foreach (var pair in multiValues)
            {
                foreach (var value in pair.Value)
                {
                    AddValue(fieldSet, valueMap, pair.Key, value);
                }
            }
        }

        if (values is not null)
        {
            foreach (var pair in values)
            {
                AddValue(fieldSet, valueMap, pair.Key, pair.Value);
            }
        }

        if (fields is not null)
        {
            foreach (var field in fields)
            {
                if (!string.IsNullOrWhiteSpace(field))
                {
                    fieldSet.Add(field);
                }
            }
        }

        return new RuleEvaluationContext
        {
            FieldValues = valueMap.ToDictionary(
                pair => pair.Key,
                pair => (IReadOnlyList<string>)pair.Value,
                StringComparer.OrdinalIgnoreCase),
            Fields = fieldSet
        };
    }

    public static IEnumerable<KeyValuePair<string, string>> ValuesFromRows(
        IEnumerable<IReadOnlyDictionary<string, string?>>? rows)
    {
        if (rows is null)
        {
            yield break;
        }

        foreach (var row in rows)
        {
            foreach (var pair in row)
            {
                if (pair.Value is not null)
                {
                    yield return new KeyValuePair<string, string>(pair.Key, pair.Value);
                }
            }
        }
    }

    private static void AddValue(
        HashSet<string> fields,
        Dictionary<string, List<string>> values,
        string key,
        string? value)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return;
        }

        fields.Add(key);
        if (value is null)
        {
            return;
        }

        if (!values.TryGetValue(key, out var list))
        {
            list = [];
            values[key] = list;
        }

        if (!list.Contains(value, StringComparer.OrdinalIgnoreCase))
        {
            list.Add(value);
        }
    }
}
