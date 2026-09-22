using Gateway.Conversations;
using Gateway.Governance;
using Gateway.Nlp.Abstractions;
using Gateway.Nlp.Cache;
using Gateway.Nlp.Embeddings;
using Gateway.Nlp.Guardrails;
using Gateway.Nlp.Llm;
using Gateway.Nlp.Mql;
using Gateway.Nlp.Orchestrator;
using Gateway.Nlp.Router;
using Gateway.Nlp.Slots;
using Gateway.Reports;
using Microsoft.Extensions.Options;

namespace Gateway.Nlp;

public static class NlpServiceCollectionExtensions
{
    public static IServiceCollection AddGatewayNlp(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<NlpOptions>(configuration.GetSection(NlpOptions.SectionName));
        services.Configure<NvidiaNimOptions>(configuration.GetSection(NvidiaNimOptions.SectionName));
        services.Configure<GovernanceOptions>(configuration.GetSection(GovernanceOptions.SectionName));
        services.Configure<ReportsOptions>(configuration.GetSection(ReportsOptions.SectionName));
        services.AddSingleton(TimeProvider.System);

        services.AddSingleton<ITextEmbedder>(sp =>
        {
            var env = sp.GetRequiredService<IHostEnvironment>();
            var options = sp.GetRequiredService<IOptions<NlpOptions>>().Value;
            var modelPath = Resolve(env, options.Embeddings.ModelPath);
            var vocabPath = Resolve(env, options.Embeddings.TokenizerPath);
            return new OnnxBgeSmallEmbedder(modelPath, vocabPath, options.Embeddings.MaxTokens);
        });

        services.AddSingleton<ISemanticCache, SemanticCache>();
        services.AddSingleton<IMqlBuilder>(_ => ScribanSimpleMqlBuilder.FromAssetsDirectory(
            Path.Combine(AppContext.BaseDirectory, "Nlp", "Assets", "Templates")));
        services.AddSingleton(Gazetteer.LoadFromDirectory(
            Path.Combine(AppContext.BaseDirectory, "Nlp", "Assets", "Gazetteers")));
        services.AddSingleton(PromptAssets.LoadFromDirectory(
            Path.Combine(AppContext.BaseDirectory, "Nlp", "Assets", "Prompts")));

        services.AddSingleton<INlpRouter>(sp => new NlpRouter(
            sp.GetRequiredService<ITextEmbedder>(),
            sp.GetRequiredService<ISemanticCache>(),
            sp.GetRequiredService<IMqlBuilder>(),
            sp.GetRequiredService<Gazetteer>()));

        services.AddSingleton<IPipelineValidator, PipelineValidator>();
        services.AddSingleton<ILlmQueryGenerator, SemanticKernelLlmQueryGenerator>();
        services.AddSingleton<SelfCorrectingLlmQueryGenerator>();
        services.AddSingleton(SchemaWhitelist.LoadFromSchemaFile(
            Path.Combine(AppContext.BaseDirectory, "Nlp", "Assets", "Prompts", "schema.txt")));
        services.AddSingleton<ISensitiveFieldRegistry>(_ => InMemorySensitiveFieldRegistry.LoadFromDirectory(
            Path.Combine(AppContext.BaseDirectory, "Nlp", "Assets", "Schema")));
        services.AddSingleton<GuardrailEvaluator>();
        services.AddSingleton<IAccessRequestStore, InMemoryAccessRequestStore>();
        services.AddSingleton<IAgentEventSink, InMemoryAgentEventSink>();
        services.AddSingleton<IAgentStateStore, InMemoryAgentStateStore>();
        services.AddSingleton<INlpOrchestrator, NlpOrchestrator>();

        services.AddSingleton<IApprovalFlagStore>(sp => new InMemoryApprovalFlagStore(
            sp.GetRequiredService<IOptions<GovernanceOptions>>().Value.ApprovalEnabled));
        services.AddSingleton<IConversationStore, InMemoryConversationStore>();
        services.AddSingleton<INotificationSender, SimulatedNotificationSender>();
        services.AddSingleton<IColumnCatalog, ColumnCatalog>();
        services.AddSingleton<ConversationOrchestrator>();

        return services;
    }

    private static string Resolve(IHostEnvironment env, string path)
    {
        return Path.IsPathRooted(path) ? path : Path.Combine(env.ContentRootPath, path);
    }
}
