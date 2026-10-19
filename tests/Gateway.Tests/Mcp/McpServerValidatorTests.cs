using Gateway.Mcp;

namespace Gateway.Tests.Mcp;

public class McpServerValidatorTests
{
    [Fact]
    public void Accepts_poc_stdio_definition()
    {
        var error = McpServerValidator.Validate(McpServerSeeder.BuildPocDefinition());
        Assert.Null(error);
    }

    [Fact]
    public void Rejects_disallowed_stdio_command()
    {
        var def = ValidStdio() with { Command = "bash" };
        var error = McpServerValidator.Validate(def);
        Assert.NotNull(error);
        Assert.Contains("command", error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Rejects_command_with_path()
    {
        var def = ValidStdio() with { Command = "/usr/bin/node" };
        var error = McpServerValidator.Validate(def);
        Assert.NotNull(error);
    }

    [Theory]
    [InlineData("arg;rm -rf /")]
    [InlineData("arg|cat")]
    [InlineData("arg && whoami")]
    [InlineData("arg$(id)")]
    [InlineData("arg`id`")]
    [InlineData("arg\nnode")]
    public void Rejects_shell_metacharacters_in_args(string arg)
    {
        var def = ValidStdio() with { Args = [arg] };
        var error = McpServerValidator.Validate(def);
        Assert.NotNull(error);
        Assert.Contains("argument", error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Rejects_http_url_with_disallowed_scheme()
    {
        var def = ValidHttp() with { Url = "ftp://localhost/mcp" };
        var error = McpServerValidator.Validate(def);
        Assert.NotNull(error);
        Assert.Contains("url", error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Rejects_http_url_with_disallowed_host()
    {
        var def = ValidHttp() with { Url = "https://evil.example/mcp" };
        var error = McpServerValidator.Validate(def);
        Assert.NotNull(error);
        Assert.Contains("host", error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Accepts_http_url_on_loopback()
    {
        var def = ValidHttp() with { Url = "http://127.0.0.1:3100/mcp" };
        Assert.Null(McpServerValidator.Validate(def));
    }

    [Fact]
    public void Rejects_write_tools_in_allow_list()
    {
        var def = ValidStdio() with { AllowedTools = ["get_schema", "drop_collection"] };
        var error = McpServerValidator.Validate(def);
        Assert.NotNull(error);
        Assert.Contains("read-only", error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Rejects_readOnly_false()
    {
        var def = ValidStdio() with { ReadOnly = false };
        var error = McpServerValidator.Validate(def);
        Assert.NotNull(error);
        Assert.Contains("readOnly", error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Rejects_env_keys_that_look_like_values()
    {
        var def = ValidStdio() with { EnvKeys = ["MONGO_URI=mongodb://secret"] };
        var error = McpServerValidator.Validate(def);
        Assert.NotNull(error);
        Assert.Contains("env", error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Classifies_known_poc_tools_as_read_only()
    {
        Assert.True(McpToolClassifier.IsReadOnly("get_schema"));
        Assert.True(McpToolClassifier.IsReadOnly("generate_pipeline"));
        Assert.True(McpToolClassifier.IsReadOnly("run_pipeline"));
        Assert.True(McpToolClassifier.IsReadOnly("ask"));
        Assert.False(McpToolClassifier.IsReadOnly("delete_document"));
        Assert.False(McpToolClassifier.IsReadOnly("admin_reset"));
        Assert.False(McpToolClassifier.IsReadOnly("write_file"));
    }

    private static McpServerDefinition ValidStdio() => new(
        "mcp_test",
        "test",
        null,
        McpTransport.Stdio,
        "node",
        ["src/poc/mcp-server.js.js"],
        null,
        ["MONGO_URI"],
        false,
        ["get_schema"],
        true,
        5000,
        McpScope.Global,
        null,
        1,
        null,
        null);

    private static McpServerDefinition ValidHttp() => ValidStdio() with
    {
        Transport = McpTransport.Http,
        Command = null,
        Args = [],
        Url = "http://localhost:3100/mcp"
    };
}
