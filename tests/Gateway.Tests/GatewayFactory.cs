using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Time.Testing;

namespace Gateway.Tests;

public class GatewayFactory : WebApplicationFactory<Program>
{
    public const string Issuer = "MultiAgentMongoNlp";
    public const string Audience = "MultiAgentMongoNlp.Spa";
    public const string SigningKey = "DEV-ONLY-CHANGE-ME-32CHARS-MINIMUM!!";

    public FakeTimeProvider Time { get; } = new(new DateTimeOffset(2026, 9, 5, 9, 15, 0, TimeSpan.Zero));

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.UseSetting("Jwt:Issuer", Issuer);
        builder.UseSetting("Jwt:Audience", Audience);
        builder.UseSetting("Jwt:SigningKey", SigningKey);
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<TimeProvider>();
            services.AddSingleton<TimeProvider>(Time);
        });
    }
}
