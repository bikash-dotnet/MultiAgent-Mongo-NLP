using System.Diagnostics;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace Gateway.Mcp;

public sealed class ProcessMcpSessionFactory : IMcpSessionFactory
{
    private readonly ILogger<ProcessMcpSessionFactory> _logger;

    public ProcessMcpSessionFactory(ILogger<ProcessMcpSessionFactory> logger)
    {
        _logger = logger;
    }

    public Task<IMcpSession> OpenAsync(McpServerDefinition server, CancellationToken cancellationToken = default)
    {
        var validation = McpServerValidator.Validate(server);
        if (validation is not null)
        {
            throw new InvalidOperationException(validation);
        }

        if (server.Transport == McpTransport.Http)
        {
            throw new NotSupportedException($"HTTP MCP transport for '{server.Id}' is not available in this host.");
        }

        _logger.LogInformation(
            "Opening MCP stdio session {ServerId} command={Command} args={Args}",
            server.Id,
            server.Command,
            string.Join(' ', server.Args));

        return Task.FromResult<IMcpSession>(new ProcessMcpSession(server));
    }
}

internal sealed class ProcessMcpSession : IMcpSession
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly Process _process;
    private int _nextId = 1;
    private bool _initialized;

    public ProcessMcpSession(McpServerDefinition server)
    {
        var start = new ProcessStartInfo
        {
            FileName = server.Command!,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        foreach (var arg in server.Args)
        {
            start.ArgumentList.Add(arg);
        }

        foreach (var key in server.EnvKeys)
        {
            var value = Environment.GetEnvironmentVariable(key);
            if (value is not null)
            {
                start.Environment[key] = value;
            }
        }

        _process = Process.Start(start)
            ?? throw new InvalidOperationException($"Failed to start MCP process '{server.Command}'.");
    }

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        if (_initialized)
        {
            return;
        }

        await SendRequestAsync("initialize", new
        {
            protocolVersion = "2024-11-05",
            capabilities = new { },
            clientInfo = new { name = "multiagent-mongo-nlp", version = "1.0.0" }
        }, cancellationToken);
        await SendNotificationAsync("notifications/initialized", cancellationToken);
        _initialized = true;
    }

    public async Task<IReadOnlyList<DiscoveredMcpTool>> ListToolsAsync(CancellationToken cancellationToken = default)
    {
        await InitializeAsync(cancellationToken);
        using var doc = await SendRequestAsync("tools/list", new { }, cancellationToken);
        if (!doc.RootElement.TryGetProperty("result", out var result)
            || !result.TryGetProperty("tools", out var tools)
            || tools.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        var list = new List<DiscoveredMcpTool>();
        foreach (var tool in tools.EnumerateArray())
        {
            var name = tool.TryGetProperty("name", out var nameEl) ? nameEl.GetString() : null;
            if (string.IsNullOrWhiteSpace(name))
            {
                continue;
            }

            var description = tool.TryGetProperty("description", out var descEl) ? descEl.GetString() : null;
            var schema = tool.TryGetProperty("inputSchema", out var schemaEl) ? schemaEl.GetRawText() : "{}";
            list.Add(new DiscoveredMcpTool(name, description, schema));
        }

        return list;
    }

    public ValueTask DisposeAsync()
    {
        try
        {
            if (!_process.HasExited)
            {
                _process.Kill(entireProcessTree: true);
            }
        }
        catch
        {
            // Best-effort teardown of a child MCP process.
        }

        _process.Dispose();
        return ValueTask.CompletedTask;
    }

    private async Task<JsonDocument> SendRequestAsync(string method, object @params, CancellationToken cancellationToken)
    {
        var id = Interlocked.Increment(ref _nextId);
        var payload = JsonSerializer.Serialize(new
        {
            jsonrpc = "2.0",
            id,
            method,
            @params
        });
        await WriteMessageAsync(payload, cancellationToken);
        var response = await ReadMessageAsync(cancellationToken);
        return JsonDocument.Parse(response);
    }

    private Task SendNotificationAsync(string method, CancellationToken cancellationToken)
    {
        var payload = JsonSerializer.Serialize(new
        {
            jsonrpc = "2.0",
            method
        });
        return WriteMessageAsync(payload, cancellationToken);
    }

    private async Task WriteMessageAsync(string payload, CancellationToken cancellationToken)
    {
        var bytes = Encoding.UTF8.GetBytes(payload);
        var header = Encoding.ASCII.GetBytes($"Content-Length: {bytes.Length}\r\n\r\n");
        await _process.StandardInput.BaseStream.WriteAsync(header, cancellationToken);
        await _process.StandardInput.BaseStream.WriteAsync(bytes, cancellationToken);
        await _process.StandardInput.BaseStream.FlushAsync(cancellationToken);
    }

    private async Task<string> ReadMessageAsync(CancellationToken cancellationToken)
    {
        var length = -1;
        while (true)
        {
            var line = await _process.StandardOutput.ReadLineAsync(cancellationToken)
                ?? throw new InvalidOperationException("MCP process closed stdout during handshake.");
            if (line.StartsWith("Content-Length:", StringComparison.OrdinalIgnoreCase))
            {
                length = int.Parse(line["Content-Length:".Length..].Trim());
            }

            if (line.Length == 0)
            {
                break;
            }
        }

        if (length < 0)
        {
            throw new InvalidOperationException("MCP process did not send Content-Length.");
        }

        var buffer = new char[length];
        var read = 0;
        while (read < length)
        {
            var n = await _process.StandardOutput.ReadAsync(buffer.AsMemory(read, length - read), cancellationToken);
            if (n == 0)
            {
                throw new InvalidOperationException("MCP process closed stdout before the message completed.");
            }

            read += n;
        }

        return new string(buffer);
    }
}
