namespace Gateway.Nlp.Guardrails;

public sealed class SchemaWhitelist
{
    private readonly List<string> _fields;

    private SchemaWhitelist(IEnumerable<string> fields)
    {
        _fields = fields
            .Where(f => !string.IsNullOrWhiteSpace(f))
            .Distinct(StringComparer.Ordinal)
            .ToList();

        if (!_fields.Contains("_id", StringComparer.Ordinal))
        {
            _fields.Add("_id");
        }
    }

    public IReadOnlyList<string> Fields => _fields;

    public static SchemaWhitelist FromFields(IEnumerable<string> fields)
    {
        return new SchemaWhitelist(fields);
    }

    public static SchemaWhitelist LoadFromSchemaFile(string path)
    {
        var fields = new List<string>();

        foreach (var rawLine in File.ReadLines(path))
        {
            var line = rawLine.Trim();
            if (!line.StartsWith("- ", StringComparison.Ordinal))
            {
                continue;
            }

            var entry = line[2..];
            var separator = entry.IndexOf(':');
            if (separator <= 0)
            {
                continue;
            }

            var field = entry[..separator].Trim();
            if (field.Length > 0)
            {
                fields.Add(field);
            }
        }

        return new SchemaWhitelist(fields);
    }

    public bool IsKnown(string fieldPath)
    {
        if (string.IsNullOrWhiteSpace(fieldPath))
        {
            return false;
        }

        return _fields.Any(known =>
            string.Equals(known, fieldPath, StringComparison.Ordinal) ||
            fieldPath.StartsWith(known + ".", StringComparison.Ordinal) ||
            known.StartsWith(fieldPath + ".", StringComparison.Ordinal));
    }

    public IReadOnlyList<string> FindUnknown(IEnumerable<string> fieldPaths)
    {
        return fieldPaths
            .Where(path => !IsKnown(path))
            .Distinct(StringComparer.Ordinal)
            .ToList();
    }
}
