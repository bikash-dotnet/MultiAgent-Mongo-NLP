using Gateway.Audit;
using Gateway.Conversations;
using Gateway.Execution;
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
using Gateway.Persistence;
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
        services.AddSingleton<IAgentEventSink, InMemoryAgentEventSink>();
        services.AddSingleton<INlpOrchestrator, NlpOrchestrator>();

        services.AddSingleton<IApprovalFlagStore>(sp => new InMemoryApprovalFlagStore(
            sp.GetRequiredService<IOptions<GovernanceOptions>>().Value.ApprovalEnabled));
        services.AddSingleton<INotificationSender, SimulatedNotificationSender>();
        services.AddSingleton<IColumnCatalog, ColumnCatalog>();
        services.AddSingleton<ConversationOrchestrator>();
        services.AddSingleton<IConversationResumeHandler>(sp => sp.GetRequiredService<ConversationOrchestrator>());
        services.AddSingleton<IGovernanceService, GovernanceService>();

        services.Configure<ExecutionOptions>(configuration.GetSection(ExecutionOptions.SectionName));
        services.Configure<EnterpriseCoreOptions>(configuration.GetSection(EnterpriseCoreOptions.SectionName));
        services.Configure<PersistenceOptions>(configuration.GetSection(PersistenceOptions.SectionName));

        services.AddSingleton<IDocumentStore>(sp =>
        {
            var environment = sp.GetRequiredService<IHostEnvironment>();
            var options = sp.GetRequiredService<IOptions<PersistenceOptions>>().Value;
            var config = sp.GetRequiredService<IConfiguration>();
            return DocumentStoreFactory.Create(
                options,
                environment.ContentRootPath,
                config["MongoDb:ConnectionString"],
                config["MongoDb:Database"] ?? "sample_airbnb");
        });

        services.AddSingleton<IAccessRequestStore, DurableAccessRequestStore>();
        services.AddSingleton<IAgentStateStore, DurableAgentStateStore>();
        services.AddSingleton<IConversationStore, DurableConversationStore>();
        services.AddSingleton<IAuditLogStore, DurableAuditLogStore>();

        services.AddHttpClient("enterprise-core", (sp, client) =>
        {
            var options = sp.GetRequiredService<IOptions<EnterpriseCoreOptions>>().Value;
            if (!string.IsNullOrWhiteSpace(options.BaseUrl))
            {
                client.BaseAddress = new Uri(options.BaseUrl);
            }

            client.Timeout = TimeSpan.FromSeconds(30);
        });

        services.AddSingleton<MongoQueryTransport>(sp => new MongoQueryTransport(
            sp.GetRequiredService<IConfiguration>()["MongoDb:ConnectionString"] ?? "mongodb://localhost:27017",
            sp.GetRequiredService<IOptions<ExecutionOptions>>()));

        services.AddSingleton<EnterpriseCoreQueryTransport>(sp => new EnterpriseCoreQueryTransport(
            sp.GetRequiredService<IHttpClientFactory>().CreateClient("enterprise-core"),
            sp.GetRequiredService<IOptions<EnterpriseCoreOptions>>()));

        services.AddSingleton<IQueryTransport>(sp => new RoutingQueryExecutor(
            sp.GetRequiredService<MongoQueryTransport>(),
            sp.GetRequiredService<EnterpriseCoreQueryTransport>(),
            sp.GetRequiredService<IOptions<ExecutionOptions>>()));

        services.AddSingleton<ITabularQueryExecutor, ExecutionRunner>();

        return services;
    }

    private static string Resolve(IHostEnvironment env, string path)
    {
        return Path.IsPathRooted(path) ? path : Path.Combine(env.ContentRootPath, path);
    }
}
