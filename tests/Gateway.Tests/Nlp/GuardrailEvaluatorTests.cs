using Gateway.Governance;
using Gateway.Nlp;
using Gateway.Nlp.Guardrails;
using Gateway.Schemas;

namespace Gateway.Tests.Nlp;

public class GuardrailEvaluatorTests
{
    private static readonly GuardrailEvaluator Evaluator = CreateEvaluator();

    private static GuardrailEvaluator CreateEvaluator()
    {
        var fields = new List<SchemaField>
        {
            new("price", SchemaFieldType.Number),
            new("amenities", SchemaFieldType.Array),
            new("address.market", SchemaFieldType.String),
            new("review_scores.rating", SchemaFieldType.Number),
            new("address.location.coordinates", SchemaFieldType.Object, true, true, ["DataOwner"]),
            new("host.host_verifications", SchemaFieldType.Array, true, true, ["DataOwner"]),
            new("host.host_identity_verified", SchemaFieldType.Boolean, true, false, ["DataOwner"])
        };

        var schema = new SchemaDefinition("sch_test", "test", SchemaDataSource.Mongo, "db", "coll", fields);
        var registry = new TestSchemaRegistry(schema);
        var provider = new ActiveSchemaProvider(registry);
        provider.RefreshAsync().GetAwaiter().GetResult();

        return new GuardrailEvaluator(provider, new InMemoryApprovalFlagStore(true));
    }

    private sealed class TestSchemaRegistry : ISchemaRegistry
    {
        private readonly IReadOnlyList<SchemaDefinition> _schemas;

        public TestSchemaRegistry(params SchemaDefinition[] schemas)
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

    [Fact]
    public void Clean_read_query_is_allowed()
    {
        var result = Evaluator.Evaluate(
            """[{"$match":{"price":{"$lte":200},"address.market":"New York"}},{"$limit":10}]""");

        Assert.Equal(GuardrailOutcome.Allowed, result.Outcome);
        Assert.Empty(result.SensitiveFields);
    }

    [Fact]
    public void Write_stage_is_rejected()
    {
        var result = Evaluator.Evaluate("""[{"$out":"archive"}]""");

        Assert.Equal(GuardrailOutcome.Rejected, result.Outcome);
        Assert.Contains("$out", result.BlockedOperators);
    }

    [Fact]
    public void Unparseable_pipeline_is_rejected()
    {
        var result = Evaluator.Evaluate("not json");

        Assert.Equal(GuardrailOutcome.Rejected, result.Outcome);
        Assert.False(string.IsNullOrWhiteSpace(result.Reason));
    }

    [Fact]
    public void Unknown_field_is_rejected()
    {
        var result = Evaluator.Evaluate("""[{"$match":{"secrets.token":"x"}}]""");

        Assert.Equal(GuardrailOutcome.Rejected, result.Outcome);
        Assert.Contains("secrets.token", result.UnknownFields);
    }

    [Fact]
    public void Sensitive_coordinates_pause_for_approval()
    {
        var result = Evaluator.Evaluate(
            """[{"$match":{"address.location.coordinates.lat":{"$gte":40}}}]""");

        Assert.Equal(GuardrailOutcome.PausedForApproval, result.Outcome);
        Assert.Contains("address.location.coordinates", result.SensitiveFields);
    }

    [Fact]
    public void Registry_known_field_not_in_schema_pauses()
    {
        var result = Evaluator.Evaluate("""[{"$match":{"host.host_verifications.0":"verified"}}]""");

        Assert.Equal(GuardrailOutcome.PausedForApproval, result.Outcome);
        Assert.Contains("host.host_verifications", result.SensitiveFields);
    }

    [Fact]
    public void Sensitive_field_without_approval_flag_is_allowed()
    {
        var result = Evaluator.Evaluate("""[{"$match":{"host.host_identity_verified":true}}]""");

        Assert.Equal(GuardrailOutcome.Allowed, result.Outcome);
    }

    [Fact]
    public void Parent_reference_to_sensitive_document_pauses()
    {
        var result = Evaluator.Evaluate("""[{"$match":{"address":"x"}}]""");

        Assert.Equal(GuardrailOutcome.PausedForApproval, result.Outcome);
        Assert.Contains("address.location.coordinates", result.SensitiveFields);
    }
}
