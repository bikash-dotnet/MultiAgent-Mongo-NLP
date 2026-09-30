using System.Security.Claims;
using Gateway.Auth;
using Gateway.Execution;
using Gateway.Governance;

namespace Gateway.Tests.Execution;

public class ExecutionContractTests
{
    [Fact]
    public void Requester_context_carries_the_email()
    {
        var principal = new ClaimsPrincipal(new ClaimsIdentity(
        [
            new Claim(SessionClaims.UserId, "usr_1"),
            new Claim(SessionClaims.Name, "Bikash"),
            new Claim(SessionClaims.Email, "bnayak@enterprise.com"),
            new Claim(SessionClaims.Role, "Business Analyst")
        ]));

        var context = SessionClaims.ToRequesterContext(principal);

        Assert.Equal("bnayak@enterprise.com", context.Email);
    }

    [Fact]
    public void Tabular_result_counts_rows_and_reports_empty()
    {
        var rows = new List<IReadOnlyDictionary<string, string?>>
        {
            new Dictionary<string, string?> { ["name"] = "a" },
            new Dictionary<string, string?> { ["name"] = "b" }
        };

        var result = new TabularResult(["name"], rows, "Mongo", 12);
        var empty = TabularResult.Empty("Mongo");

        Assert.Equal(2, result.RowCount);
        Assert.Equal(0, empty.RowCount);
        Assert.False(result.TimedOut);
    }
}
