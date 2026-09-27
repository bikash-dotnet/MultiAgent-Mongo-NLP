namespace Gateway.Governance;

public static class RoleNormalizer
{
    public static string Normalize(string role)
    {
        if (string.IsNullOrWhiteSpace(role))
        {
            return string.Empty;
        }

        return new string(role.Where(char.IsLetterOrDigit).Select(char.ToLowerInvariant).ToArray());
    }

    public static bool Matches(string claimRole, string ownerRole)
    {
        var claim = Normalize(claimRole);
        var owner = Normalize(ownerRole);
        if (claim.Length == 0 || owner.Length == 0)
        {
            return false;
        }

        return claim.Contains(owner, StringComparison.Ordinal) || owner.Contains(claim, StringComparison.Ordinal);
    }
}
