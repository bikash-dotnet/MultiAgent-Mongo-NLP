using Gateway.Reports;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Gateway.Tests.Reports;

public class ReportsOptionsTests
{
    [Fact]
    public void Production_disables_the_demo_fallback()
    {
        var configuration = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json")
            .AddJsonFile("appsettings.Production.json")
            .Build();

        var services = new ServiceCollection();
        services.Configure<ReportsOptions>(configuration.GetSection(ReportsOptions.SectionName));
        using var provider = services.BuildServiceProvider();

        Assert.False(provider.GetRequiredService<IOptions<ReportsOptions>>().Value.DemoFallbackEnabled);
    }

    [Fact]
    public void The_default_keeps_the_demo_fallback_enabled()
    {
        var configuration = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json")
            .Build();

        var services = new ServiceCollection();
        services.Configure<ReportsOptions>(configuration.GetSection(ReportsOptions.SectionName));
        using var provider = services.BuildServiceProvider();

        Assert.True(provider.GetRequiredService<IOptions<ReportsOptions>>().Value.DemoFallbackEnabled);
    }
}
