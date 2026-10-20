using Gateway.Audit;
using Gateway.Conversations;
using Gateway.Execution;
using Gateway.Mcp;
using Gateway.Nlp.Guardrails;
using Gateway.Nlp.Http;
using Gateway.Nlp.Orchestrator;
using Gateway.Persistence;
using Gateway.Rules;
using Gateway.Schemas;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;

namespace Gateway.Tests.Persistence;

public class MongoBsonConfigurationTests
{
    public MongoBsonConfigurationTests()
    {
        MongoBsonConfiguration.EnsureRegistered();
    }

    [Fact]
    public void BusinessRule_maps_id_to_underscore_id_and_serializes_enums_as_strings()
    {
        var rule = new BusinessRule(
            "br_1",
            "Cap rows",
            null,
            RuleScope.Global,
            null,
            1,
            true,
            new RuleCondition("price", ">", ["10"]),
            new RuleAction(RuleActions.CapRows, new Dictionary<string, string> { ["maxRows"] = "100" }),
            1,
            DateTimeOffset.UnixEpoch,
            "seed");

        var document = rule.ToBsonDocument();

        Assert.Equal("br_1", document["_id"].AsString);
        Assert.False(document.Contains("id"));
        Assert.Equal("Global", document["scope"].AsString);
        Assert.Equal(BsonType.DateTime, document["updatedAt"].BsonType);
        Assert.Equal("100", document["action"]["parameters"]["maxRows"].AsString);

        var reloaded = BsonSerializer.Deserialize<BusinessRule>(document);
        Assert.Equal("br_1", reloaded.Id);
        Assert.Equal(RuleScope.Global, reloaded.Scope);
        Assert.Equal("100", reloaded.Action.Parameters["maxRows"]);
    }

    [Fact]
    public void Schema_round_trips_fields_and_enum_values()
    {
        var schema = new SchemaDefinition(
            "sch_1",
            "Listings",
            SchemaDataSource.Mongo,
            "sample_airbnb",
            "listingsAndReviews",
            [new SchemaField("address.market", SchemaFieldType.String), new SchemaField("price", SchemaFieldType.Number)],
            IsActiveSource: true);

        var document = schema.ToBsonDocument();

        Assert.Equal("sch_1", document["_id"].AsString);
        Assert.False(document.Contains("id"));
        Assert.Equal("Mongo", document["dataSource"].AsString);
        Assert.Equal("String", document["fields"][0]["dataType"].AsString);

        var reloaded = BsonSerializer.Deserialize<SchemaDefinition>(document);
        Assert.Equal("sch_1", reloaded.Id);
        Assert.Equal(SchemaDataSource.Mongo, reloaded.DataSource);
        Assert.Equal(SchemaFieldType.Number, reloaded.Fields[1].DataType);
        Assert.Equal("address.market", reloaded.Fields[0].Path);
    }

    [Fact]
    public void Audit_document_round_trips_dictionary_and_list_members()
    {
        var audit = new AuditLogDocument(
            "aud_1",
            DateTimeOffset.UnixEpoch,
            "session-1",
            "Mongo",
            new AuditLogUser("user-1", "Ada", "ada@example.com", "Admin"),
            new AuditLogNlpPerformance(false, true, 12, 40),
            new AuditLogRequestDetails("average price", new Dictionary<string, string> { ["market"] = "Paris" }),
            new AuditLogExecutionDetails("[]", "listingsAndReviews", 1, null, null),
            new AuditLogGovernance(false, ["price"], null, false, null),
            null,
            null);

        var document = audit.ToBsonDocument();

        Assert.Equal("aud_1", document["_id"].AsString);
        Assert.False(document.Contains("id"));

        var reloaded = BsonSerializer.Deserialize<AuditLogDocument>(document);
        Assert.Equal("aud_1", reloaded.Id);
        Assert.Equal("Paris", reloaded.RequestDetails.ClarificationsApplied["market"]);
        Assert.Contains("price", reloaded.Governance.FlagsTriggered);
    }

    [Fact]
    public void Agent_state_uses_session_id_as_the_mongo_id()
    {
        var state = new AgentState("session-1", "governance_paused", "needs approval", DateTimeOffset.UnixEpoch, "req-1");

        var document = state.ToBsonDocument();

        Assert.Equal("session-1", document["_id"].AsString);
        Assert.False(document.Contains("sessionId"));

        var reloaded = BsonSerializer.Deserialize<AgentState>(document);
        Assert.Equal("session-1", reloaded.SessionId);
        Assert.Equal("req-1", reloaded.AccessRequestId);
    }

    [Fact]
    public void Conversation_state_round_trips_nested_enums()
    {
        var state = new ConversationState(
            "conv_1",
            "session-1",
            ConversationStep.Complete,
            "average price",
            "[{\"$match\":{}}]",
            Gateway.Nlp.Router.NlpRouteKind.SimpleMql,
            null,
            [],
            false,
            false,
            new ReportIntakeDraft(),
            DateTimeOffset.UnixEpoch,
            DateTimeOffset.UnixEpoch,
            Result: new NlpQueryResponse("SimpleMql", "[]", null, false, false, "simple", true, 0, 0, null, null, null, null),
            Execution: new TabularResult(["name"], [new Dictionary<string, string?> { ["name"] = "alpha" }], "Mongo", 5));

        var document = state.ToBsonDocument();

        Assert.Equal("conv_1", document["_id"].AsString);
        Assert.False(document.Contains("id"));
        Assert.Equal("Complete", document["step"].AsString);
        Assert.Equal("SimpleMql", document["kind"].AsString);

        var reloaded = BsonSerializer.Deserialize<ConversationState>(document);
        Assert.Equal("conv_1", reloaded.Id);
        Assert.Equal(ConversationStep.Complete, reloaded.Step);
        Assert.Equal(Gateway.Nlp.Router.NlpRouteKind.SimpleMql, reloaded.Kind);
        Assert.Equal("alpha", reloaded.Execution!.Rows[0]["name"]);
    }

    [Fact]
    public void Access_request_round_trips_nested_intake_and_resolution()
    {
        var request = new AccessRequest(
            "req_1",
            "session-1",
            "[{\"$match\":{}}]",
            ["price"],
            AccessRequest.Approved,
            "justify",
            DateTimeOffset.UnixEpoch,
            new ReportIntake("ada@example.com", "analysis", null, "lead@example.com", ["price"], ReportIntake.Csv),
            new RequesterInfo("user-1", "Ada", "DataOwner", "lead-1"),
            "lead-1",
            [new RequestedFlag("price", "sensitive")],
            new GovernanceJustification("analysis", "impact", null),
            new ApprovalResolution("lead-1", "admin-1", "Admin", "Admin", false, null, DateTimeOffset.UnixEpoch, "ok"));

        var document = request.ToBsonDocument();

        Assert.Equal("req_1", document["_id"].AsString);
        Assert.False(document.Contains("id"));

        var reloaded = BsonSerializer.Deserialize<AccessRequest>(document);
        Assert.Equal("ada@example.com", reloaded.Intake!.RequesterEmail);
        Assert.Equal("price", reloaded.Intake.Columns[0]);
        Assert.Equal("sensitive", reloaded.RequestedFlags![0].Flag);
        Assert.Equal("ok", reloaded.Resolution!.Notes);
        Assert.Equal(AccessRequest.Approved, reloaded.Status);
    }

    [Fact]
    public void Mcp_server_round_trips_read_only_lists()
    {
        var server = new McpServerDefinition(
            "mcp_1",
            "reference",
            null,
            McpTransport.Stdio,
            "node",
            ["server.js"],
            null,
            ["API_KEY"],
            true,
            ["ask", "get_schema"],
            true,
            5_000,
            McpScope.Global,
            null,
            1,
            DateTimeOffset.UnixEpoch,
            "seed");

        var document = server.ToBsonDocument();

        Assert.Equal("mcp_1", document["_id"].AsString);
        Assert.False(document.Contains("id"));
        Assert.Equal("Stdio", document["transport"].AsString);

        var reloaded = BsonSerializer.Deserialize<McpServerDefinition>(document);
        Assert.Equal(["server.js"], reloaded.Args);
        Assert.Equal(["ask", "get_schema"], reloaded.AllowedTools);
        Assert.Equal(McpTransport.Stdio, reloaded.Transport);
    }

    [Fact]
    public void Activation_lock_uses_id_as_the_mongo_id()
    {
        var activationLock = new ActivationLock("_activation_lock", "token-1", DateTimeOffset.UnixEpoch);

        var document = activationLock.ToBsonDocument();

        Assert.Equal("_activation_lock", document["_id"].AsString);
        Assert.False(document.Contains("id"));
        Assert.Equal(BsonType.DateTime, document["expiresAt"].BsonType);

        var reloaded = BsonSerializer.Deserialize<ActivationLock>(document);
        Assert.Equal("_activation_lock", reloaded.Id);
        Assert.Equal("token-1", reloaded.Token);
    }

    [Fact]
    public void Timestamps_are_stored_as_utc_bson_dates()
    {
        var original = new DateTimeOffset(2026, 10, 19, 14, 30, 0, TimeSpan.FromHours(5));
        var rule = new BusinessRule(
            "br_1",
            "Cap rows",
            null,
            RuleScope.Global,
            null,
            1,
            true,
            new RuleCondition("price", ">", ["10"]),
            new RuleAction(RuleActions.CapRows, new Dictionary<string, string> { ["maxRows"] = "100" }),
            1,
            original,
            "seed");

        var document = rule.ToBsonDocument();

        Assert.Equal(BsonType.DateTime, document["updatedAt"].BsonType);
        Assert.Equal(original.UtcDateTime, document["updatedAt"].AsBsonDateTime.ToUniversalTime());

        var reloaded = BsonSerializer.Deserialize<BusinessRule>(document);
        Assert.Equal(TimeSpan.Zero, reloaded.UpdatedAt!.Value.Offset);
        Assert.Equal(original, reloaded.UpdatedAt.Value);
    }
}
