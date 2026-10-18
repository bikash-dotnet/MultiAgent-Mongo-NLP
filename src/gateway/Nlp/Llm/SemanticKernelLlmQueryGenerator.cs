using System.Text.Json;
using Gateway.Configuration;
using Microsoft.Extensions.Options;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;

namespace Gateway.Nlp.Llm;

public sealed class LlmNotConfiguredException : InvalidOperationException
{
    public LlmNotConfiguredException()
        : base("NVIDIA NIM is not configured. Set NvidiaNim:ApiKey (env NvidiaNim__ApiKey).")
    {
    }
}

public sealed class SemanticKernelLlmQueryGenerator : ILlmQueryGenerator
{
    private readonly NvidiaNimOptions _options;
    private readonly ISettingsProvider _settings;
    private readonly PromptAssets _assets;
    private readonly ILogger<SemanticKernelLlmQueryGenerator> _logger;
    private Lazy<IChatCompletionService> _chat;

    public SemanticKernelLlmQueryGenerator(
        IOptions<NvidiaNimOptions> options,
        ISettingsProvider settings,
        PromptAssets assets,
        ILogger<SemanticKernelLlmQueryGenerator> logger)
    {
        _options = options.Value;
        _settings = settings;
        _assets = assets;
        _logger = logger;
        _chat = CreateChatLazy();

        settings.Changed += OnSettingsChanged;
    }

    private void OnSettingsChanged(string sectionName)
    {
        if (string.Equals(sectionName, "NvidiaNim", StringComparison.Ordinal) ||
            string.Equals(sectionName, "*", StringComparison.Ordinal))
        {
            _chat = CreateChatLazy();
        }
    }

    private Lazy<IChatCompletionService> CreateChatLazy() => new(CreateChatService);

    public async Task<LlmQueryResult> GenerateAsync(
        string utterance,
        string slotsJson,
        string? previousError,
        CancellationToken cancellationToken)
    {
        if (!_options.IsConfigured)
        {
            throw new LlmNotConfiguredException();
        }

        var prompt = _assets.BuildPrompt(utterance, slotsJson, previousError);
        var history = new ChatHistory();
        history.AddUserMessage(prompt);

        var settings = new OpenAIPromptExecutionSettings
        {
            Temperature = 0,
            MaxTokens = 1024
        };

        var response = await _chat.Value.GetChatMessageContentAsync(history, settings, cancellationToken: cancellationToken);
        var content = response.Content ?? string.Empty;
        _logger.LogInformation("NIM generated {Chars} chars of pipeline", content.Length);
        return new LlmQueryResult(content, EstimateTokens(prompt, content));
    }

    private IChatCompletionService CreateChatService()
    {
        var model = _options.Model;
        var baseUrl = _options.BaseUrl;
        var apiKey = _options.ApiKey;

        if (_settings.TryGetValue("NvidiaNim", "Model", out var modelValue) &&
            modelValue.ValueKind == JsonValueKind.String &&
            !string.IsNullOrWhiteSpace(modelValue.GetString()))
        {
            model = modelValue.GetString()!;
        }

        if (_settings.TryGetValue("NvidiaNim", "BaseUrl", out var baseUrlValue) &&
            baseUrlValue.ValueKind == JsonValueKind.String &&
            !string.IsNullOrWhiteSpace(baseUrlValue.GetString()))
        {
            baseUrl = baseUrlValue.GetString()!;
        }

        var builder = Kernel.CreateBuilder();
        builder.AddOpenAIChatCompletion(
            modelId: model,
            endpoint: new Uri(baseUrl),
            apiKey: apiKey);
        return builder.Build().GetRequiredService<IChatCompletionService>();
    }

    private static int EstimateTokens(string prompt, string completion)
    {
        return (prompt.Length + completion.Length) / 4;
    }
}