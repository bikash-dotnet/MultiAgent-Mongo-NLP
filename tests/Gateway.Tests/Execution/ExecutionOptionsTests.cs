using Gateway.Execution;
using Gateway.Persistence;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Gateway.Tests.Execution;

public class ExecutionOptionsTests
{
    [Fact]
    public void Execution_defaults_match_the_brd()
    {
        var options = new ExecutionOptions();

        Assert.Equal(ExecutionDataSource.Mongo, options.DataSource);
        Assert.Equal(5000, options.TimeoutMs);
        Assert.Equal("listingsAndReviews", options.Collection);
        Assert.Equal("sample_airbnb", options.Database);
    }

    [Fact]
    public void Persistence_defaults_to_auto()
    {
        var options = new PersistenceOptions();

        Assert.Equal("auto", options.Mode);
        Assert.Equal(".data", options.DataDirectory);
    }

    [Fact]
    public void Execution_section_binds_from_configuration()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Execution:DataSource"] = "EnterpriseCoreREST",
                ["Execution:TimeoutMs"] = "2500",
                ["Execution:Collection"] = "other",
                ["Execution:Database"] = "otherdb",
                ["EnterpriseCore:BaseUrl"] = "http://localhost:5000",
                ["EnterpriseCore:QueryPath"] = "/api/query",
                ["Persistence:Mode"] = "file"
            })
            .Build();

        var services = new ServiceCollection();
        services.Configure<ExecutionOptions>(configuration.GetSection(ExecutionOptions.SectionName));
        services.Configure<EnterpriseCoreOptions>(configuration.GetSection(EnterpriseCoreOptions.SectionName));
        services.Configure<PersistenceOptions>(configuration.GetSection(PersistenceOptions.SectionName));
        using var provider = services.BuildServiceProvider();

        var execution = provider.GetRequiredService<IOptions<ExecutionOptions>>().Value;
        var enterprise = provider.GetRequiredService<IOptions<EnterpriseCoreOptions>>().Value;
        var persistence = provider.GetRequiredService<IOptions<PersistenceOptions>>().Value;

        Assert.Equal(ExecutionDataSource.EnterpriseCoreREST, execution.DataSource);
        Assert.Equal(2500, execution.TimeoutMs);
        Assert.Equal("otherdb", execution.Database);
        Assert.Equal("http://localhost:5000", enterprise.BaseUrl);
        Assert.Equal("/api/query", enterprise.QueryPath);
        Assert.Equal("file", persistence.Mode);
    }
}
