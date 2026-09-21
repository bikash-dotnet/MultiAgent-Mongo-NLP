using Gateway.Nlp.Guardrails;

namespace Gateway.Tests.Nlp;

public class ReportIntakeTests
{
    [Fact]
    public async Task Update_replaces_a_stored_request_with_the_intake()
    {
        var store = new InMemoryAccessRequestStore();
        var created = await store.CreateAsync(new AccessRequest(
            string.Empty,
            "sess_1",
            "[{\"$match\":{}}]",
            ["address.location.coordinates"],
            AccessRequest.PendingLead,
            null,
            DateTimeOffset.UnixEpoch));

        var intake = new ReportIntake(
            "analyst@enterprise.com",
            "Quarterly geo analysis",
            "PROJ-GEO",
            "manager@enterprise.com",
            ["name", "price"],
            ReportIntake.Csv);

        var updated = await store.UpdateAsync(created with { Intake = intake });

        var fetched = await store.GetAsync(created.Id);
        Assert.NotNull(fetched);
        Assert.NotNull(fetched!.Intake);
        Assert.Equal("manager@enterprise.com", fetched.Intake!.ManagerEmail);
        Assert.Equal("PROJ-GEO", fetched.Intake.ProjectCode);
        Assert.Equal(created.Id, updated.Id);
    }
}
