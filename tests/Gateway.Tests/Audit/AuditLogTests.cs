using System.Reflection;
using Gateway.Audit;
using Gateway.Execution;
using Gateway.Governance;
using Gateway.Persistence;

namespace Gateway.Tests.Audit;

public class AuditLogTests
{
    [Fact]
    public async Task Append_then_list_round_trips_and_assigns_an_id()
    {
        var store = new InMemoryAuditLogStore();
        var document = BuildDocument();

        var appended = await store.AppendAsync(document with { Id = string.Empty }, CancellationToken.None);
        var listed = await store.ListAsync(CancellationToken.None);

        Assert.False(string.IsNullOrWhiteSpace(appended.Id));
        Assert.Single(listed);
        Assert.Equal(appended.Id, listed[0].Id);
    }

    [Fact]
    public async Task Durable_store_persists_across_instances()
    {
        var backend = new InMemoryDocumentStore();
        var document = BuildDocument();

        var appended = await new DurableAuditLogStore(backend).AppendAsync(document with { Id = string.Empty }, CancellationToken.None);
        var reloaded = await new DurableAuditLogStore(backend).GetAsync(appended.Id, CancellationToken.None);

        Assert.NotNull(reloaded);
        Assert.Equal("usr_1", reloaded!.User.UserId);
    }

    [Fact]
    public void Audit_store_exposes_no_update_or_delete_path()
    {
        var interfaces = new[] { typeof(IAuditLogStore) };
        var implementations = new[] { typeof(DurableAuditLogStore), typeof(InMemoryAuditLogStore) };

        foreach (var method in interfaces.SelectMany(type => type.GetMethods()).Concat(implementations.SelectMany(type => type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))))
        {
            Assert.DoesNotContain("update", method.Name, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("delete", method.Name, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("remove", method.Name, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public void Factory_maps_the_full_brd_6_1_shape()
    {
        var result = new TabularResult(["price"], [], "Mongo", 184, Error: "boom");

        var document = AuditLogFactory.Build("audit-1", DateTimeOffset.UnixEpoch, BuildRequest(), result);

        Assert.Equal("sess-1", document.SessionId);
        Assert.Equal("Mongo", document.DataSource);
        Assert.Equal("usr_1", document.User.UserId);
        Assert.Equal("bnayak@enterprise.com", document.User.Email);
        Assert.True(document.Governance.SensitiveDataAccessed);
        Assert.Equal("EXEMPTION_OWNER_ACCESS", document.Governance.ExemptionType);
        Assert.Equal("Bikash", document.Governance.AuthorizedBy);
        Assert.Equal(0, document.ExecutionDetails.RowsReturned);
        Assert.Equal("boom", document.ExecutionDetails.Error);
        Assert.Equal(184, document.NlpPerformance.ExecutionDurationMs);
        Assert.Equal("new-york", document.RequestDetails.ClarificationsApplied["market"]);
    }

    private static ExecutionRequest BuildRequest()
    {
        return new ExecutionRequest(
            "[{\"$match\":{}}]",
            "sess-1",
            "Listings in New York",
            ExecutionDataSource.Mongo,
            "listingsAndReviews",
            ["price"],
            new Dictionary<string, string> { ["market"] = "new-york" },
            false,
            true,
            42,
            new RequesterContext("usr_1", "Bikash", "Data Owner / Admin", null, "bnayak@enterprise.com"),
            new GovernanceDecision(true, ["price"], "EXEMPTION_OWNER_ACCESS", false, "Bikash"));
    }

    private static AuditLogDocument BuildDocument()
    {
        return AuditLogFactory.Build(string.Empty, DateTimeOffset.UnixEpoch, BuildRequest(), TabularResult.Empty("Mongo"));
    }
}
