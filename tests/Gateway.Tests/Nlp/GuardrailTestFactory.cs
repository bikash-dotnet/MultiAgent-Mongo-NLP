using Gateway.Governance;
using Gateway.Nlp;
using Gateway.Nlp.Guardrails;
using Gateway.Rules;
using Gateway.Schemas;
using Gateway.Persistence;

namespace Gateway.Tests.Nlp;

internal static class GuardrailTestFactory
{
    public static GuardrailEvaluator FromAssets(IApprovalFlagStore? flags = null, IBusinessRuleStore? ruleStore = null)
    {
        var engine = ruleStore is not null ? new BusinessRuleEngine(ruleStore)
            : new BusinessRuleEngine(new BusinessRuleStore(new InMemoryDocumentStore()));

        var whitelist = SchemaWhitelist.LoadFromSchemaFile(
            Path.Combine(AppContext.BaseDirectory, "Nlp", "Assets", "Prompts", "schema.txt"));
        var flagRegistry = InMemorySensitiveFieldRegistry.LoadFromDirectory(
            Path.Combine(AppContext.BaseDirectory, "Nlp", "Assets", "Schema"));

        var fields = new List<SchemaField>();
        foreach (var path in whitelist.Fields)
        {
            var flag = flagRegistry.All.FirstOrDefault(f => f.Path == path);
            fields.Add(new SchemaField(
                path, SchemaFieldType.String,
                flag?.IsSensitive ?? false,
                flag?.RequiresApproval ?? false,
                flag?.DataOwnerRoles ?? []));
        }

        foreach (var flag in flagRegistry.All)
        {
            if (!whitelist.IsKnown(flag.Path))
            {
                fields.Add(new SchemaField(
                    flag.Path, SchemaFieldType.String,
                    flag.IsSensitive, flag.RequiresApproval, flag.DataOwnerRoles));
            }
        }

        var schema = new SchemaDefinition("sch_assets", "assets", SchemaDataSource.Mongo, "db", "coll", fields);
        var fakeRegistry = new FakeSchemaRegistry(schema);
        var provider = new ActiveSchemaProvider(fakeRegistry);
        provider.RefreshAsync().GetAwaiter().GetResult();

        return new GuardrailEvaluator(provider, flags ?? new InMemoryApprovalFlagStore(true), engine);
    }

    private sealed class FakeSchemaRegistry : ISchemaRegistry
    {
        private readonly IReadOnlyList<SchemaDefinition> _schemas;

        public FakeSchemaRegistry(params SchemaDefinition[] schemas)
        {
            _schemas = schemas;
        }

        public Task<IReadOnlyList<SchemaDefinition>> ListAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_schemas);
        }

        public Task<SchemaDefinition?> GetAsync(string id, CancellationToken cancellationToken = default)
        {
            return Task.FromResult<SchemaDefinition?>(null);
        }

        public Task<SchemaDefinition?> GetActiveAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult<SchemaDefinition?>(
                _schemas.FirstOrDefault(schema => schema.IsActiveSource) ?? _schemas.FirstOrDefault());
        }

        public Task<SchemaDefinition> CreateAsync(SchemaDefinition schema, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(schema);
        }

        public Task<SchemaDefinition> SaveAsync(SchemaDefinition schema, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(schema);
        }

        public Task<bool> DeleteAsync(string id, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(false);
        }

        public Task SeedAsync(CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task ActivateAsync(string id, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }
    }
}
