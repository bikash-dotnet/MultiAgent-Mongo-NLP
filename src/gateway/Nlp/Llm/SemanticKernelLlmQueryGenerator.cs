using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
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
    private readonly PromptAssets _assets;
    private readonly ILogger<SemanticKernelLlmQueryGenerator> _logger;
    private readonly Lazy<IChatCompletionService> _chat;

    public SemanticKernelLlmQueryGenerator(
        IOptions<NvidiaNimOptions> options,
        PromptAssets assets,
        ILogger<SemanticKernelLlmQueryGenerator> logger)
    {
        _options = options.Value;
        _assets = assets;
        _logger = logger;
        _chat = new Lazy<IChatCompletionService>(CreateChatService);
    }

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
        var builder = Kernel.CreateBuilder();
        builder.AddOpenAIChatCompletion(
            modelId: _options.Model,
            endpoint: new Uri(_options.BaseUrl),
            apiKey: _options.ApiKey);
        return builder.Build().GetRequiredService<IChatCompletionService>();
    }

    private static int EstimateTokens(string prompt, string completion)
    {
        return (prompt.Length + completion.Length) / 4;
    }
}
