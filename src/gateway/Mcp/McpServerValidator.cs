using System.Text.RegularExpressions;

namespace Gateway.Mcp;

public static class McpServerValidator
{
    public static readonly HashSet<string> AllowedCommands = new(StringComparer.OrdinalIgnoreCase)
    {
        "node", "dotnet", "npx"
    };

    public static readonly HashSet<string> AllowedHosts = new(StringComparer.OrdinalIgnoreCase)
    {
        "localhost", "127.0.0.1", "::1", "[::1]"
    };

    private static readonly Regex EnvKeyPattern = new("^[A-Za-z_][A-Za-z0-9_]*$", RegexOptions.Compiled);
    private static readonly char[] ShellMetacharacters = [';', '|', '&', '$', '`', '\n', '\r', '<', '>', '(', ')', '{', '}'];

    public static string? Validate(McpServerDefinition server)
    {
        if (string.IsNullOrWhiteSpace(server.Name))
        {
            return "Name is required.";
        }

        if (!server.ReadOnly)
        {
            return "readOnly must remain true; write/admin MCP servers are not permitted.";
        }

        if (server.TimeoutMs <= 0)
        {
            return "timeoutMs must be a positive integer.";
        }

        if (server.Transport == McpTransport.Stdio)
        {
            var commandError = ValidateCommand(server.Command);
            if (commandError is not null)
            {
                return commandError;
            }

            var argsError = ValidateArgs(server.Args);
            if (argsError is not null)
            {
                return argsError;
            }
        }
        else
        {
            var urlError = ValidateUrl(server.Url);
            if (urlError is not null)
            {
                return urlError;
            }
        }

        var envError = ValidateEnvKeys(server.EnvKeys);
        if (envError is not null)
        {
            return envError;
        }

        foreach (var tool in server.AllowedTools)
        {
            if (!McpToolClassifier.IsReadOnly(tool))
            {
                return $"Tool '{tool}' is not a read-only tool and cannot be enabled.";
            }
        }

        return null;
    }

    private static string? ValidateCommand(string? command)
    {
        if (string.IsNullOrWhiteSpace(command))
        {
            return "Stdio transport requires a command.";
        }

        if (command.IndexOfAny(['/', '\\']) >= 0)
        {
            return "Stdio command must be a bare executable name, not a path.";
        }

        if (command.IndexOfAny(ShellMetacharacters) >= 0)
        {
            return "Stdio command contains disallowed shell metacharacters.";
        }

        if (!AllowedCommands.Contains(command))
        {
            return $"Stdio command '{command}' is not on the allow-list (node, dotnet, npx).";
        }

        return null;
    }

    private static string? ValidateArgs(IReadOnlyList<string> args)
    {
        foreach (var arg in args)
        {
            if (arg.IndexOfAny(ShellMetacharacters) >= 0)
            {
                return "Stdio argument contains disallowed shell metacharacters.";
            }
        }

        return null;
    }

    private static string? ValidateUrl(string? url)
    {
        if (string.IsNullOrWhiteSpace(url) || !Uri.TryCreate(url, UriKind.Absolute, out var uri))
        {
            return "HTTP transport requires a valid absolute URL.";
        }

        if (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)
        {
            return "HTTP URL scheme must be http or https.";
        }

        if (!AllowedHosts.Contains(uri.Host))
        {
            return $"HTTP URL host '{uri.Host}' is not on the allow-list.";
        }

        return null;
    }

    private static string? ValidateEnvKeys(IReadOnlyList<string> envKeys)
    {
        foreach (var key in envKeys)
        {
            if (string.IsNullOrWhiteSpace(key) || key.Contains('=') || !EnvKeyPattern.IsMatch(key))
            {
                return "envKeys must be environment variable names only; values are never stored.";
            }
        }

        return null;
    }
}
