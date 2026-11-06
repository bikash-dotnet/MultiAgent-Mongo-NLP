using Gateway.Governance;

namespace Gateway.Rules;

public static class RuleConditionEvaluator
{
    public static bool Matches(
        RuleCondition condition,
        RequesterContext requester,
        RuleEvaluationContext context)
    {
        if (IsAlwaysTrue(condition))
        {
            return true;
        }

        var op = Normalize(condition.Operator);
        if (op.Length == 0)
        {
            return false;
        }

        if (op == "matchesdataownerrole")
        {
            return MatchesDataOwnerRole(requester, condition.Values);
        }

        if (string.IsNullOrWhiteSpace(condition.Field) || condition.Field == "*")
        {
            return false;
        }

        if (!TryResolveField(condition.Field, context, out var present, out var fieldValues) || !present)
        {
            return false;
        }

        var expected = condition.Values ?? [];
        if (IsWildcardValues(expected))
        {
            return op is "equals" or "eq" or "in" or "contains";
        }

        if (fieldValues.Count == 0)
        {
            return false;
        }

        return fieldValues.Any(value => Compare(op, value, expected, condition.Combine));
    }

    internal static bool IsAlwaysTrue(RuleCondition condition)
    {
        var field = condition.Field?.Trim() ?? string.Empty;
        var op = Normalize(condition.Operator);
        var wildcardField = field.Length == 0 || field == "*";
        var wildcardOperator = op.Length == 0 || op is "equals" or "eq";
        return wildcardField && wildcardOperator && IsWildcardValues(condition.Values);
    }

    private static bool Compare(
        string op,
        string fieldValue,
        IReadOnlyList<string> expected,
        string? combine)
    {
        var combineAny = string.Equals(combine, "any", StringComparison.OrdinalIgnoreCase);
        return op switch
        {
            "equals" or "eq" => MatchesValues(fieldValue, expected, combineAny, EqualTo),
            "notequals" or "ne" => !MatchesValues(fieldValue, expected, combineAny: true, EqualTo),
            "in" => expected.Any(candidate => EqualTo(fieldValue, candidate)),
            "notin" => expected.All(candidate => !EqualTo(fieldValue, candidate)),
            "contains" => MatchesValues(fieldValue, expected, combineAny, Contains),
            "greaterthan" or "gt" => MatchesValues(fieldValue, expected, combineAny, GreaterThan),
            "lessthan" or "lt" => MatchesValues(fieldValue, expected, combineAny, LessThan),
            _ => false
        };
    }

    private static bool MatchesDataOwnerRole(RequesterContext requester, IReadOnlyList<string> values)
    {
        if (values is null || values.Count == 0 || IsWildcardValues(values))
        {
            return RoleNormalizer.Matches(requester.Role, "DataOwner")
                || RoleNormalizer.Matches(requester.Role, "Admin");
        }

        return values.Any(value => RoleNormalizer.Matches(requester.Role, value));
    }

    private static bool TryResolveField(
        string field,
        RuleEvaluationContext context,
        out bool present,
        out IReadOnlyList<string> values)
    {
        if (context.FieldValues.TryGetValue(field, out var mapped) && mapped.Count > 0)
        {
            present = true;
            values = mapped;
            return true;
        }

        if (context.Fields.Contains(field))
        {
            present = true;
            values = [];
            return true;
        }

        present = false;
        values = [];
        return false;
    }

    private static bool MatchesValues(
        string fieldValue,
        IReadOnlyList<string> values,
        bool combineAny,
        Func<string, string, bool> comparison)
    {
        if (values.Count == 0)
        {
            return false;
        }

        return combineAny
            ? values.Any(candidate => comparison(fieldValue, candidate))
            : values.All(candidate => comparison(fieldValue, candidate));
    }

    private static bool EqualTo(string fieldValue, string candidate)
    {
        if (string.Equals(fieldValue, candidate, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return TryParseDecimal(fieldValue, out var left)
            && TryParseDecimal(candidate, out var right)
            && left == right;
    }

    private static bool Contains(string fieldValue, string candidate) =>
        fieldValue.Contains(candidate, StringComparison.OrdinalIgnoreCase);

    private static bool GreaterThan(string fieldValue, string candidate) =>
        TryParseDecimal(fieldValue, out var left)
        && TryParseDecimal(candidate, out var right)
        && left > right;

    private static bool LessThan(string fieldValue, string candidate) =>
        TryParseDecimal(fieldValue, out var left)
        && TryParseDecimal(candidate, out var right)
        && left < right;

    private static bool TryParseDecimal(string value, out decimal parsed) =>
        decimal.TryParse(
            value,
            System.Globalization.NumberStyles.Any,
            System.Globalization.CultureInfo.InvariantCulture,
            out parsed);

    private static bool IsWildcardValues(IReadOnlyList<string>? values) =>
        values is null
        || values.Count == 0
        || values.All(value => string.IsNullOrWhiteSpace(value) || value.Trim() == "*");

    private static string Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        return new string(value.Where(char.IsLetterOrDigit).Select(char.ToLowerInvariant).ToArray());
    }
}
