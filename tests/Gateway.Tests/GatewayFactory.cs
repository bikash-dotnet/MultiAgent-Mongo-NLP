using Gateway.Execution;
using Gateway.Reports;
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

    public string DataDirectory { get; } = Path.Combine(Path.GetTempPath(), "gateway-tests", Guid.NewGuid().ToString("N"));

    public CapturingEmailSender Emails { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.UseSetting("Jwt:Issuer", Issuer);
        builder.UseSetting("Jwt:Audience", Audience);
        builder.UseSetting("Jwt:SigningKey", SigningKey);
        builder.UseSetting("Persistence:Mode", "file");
        builder.UseSetting("Persistence:DataDirectory", DataDirectory);
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<TimeProvider>();
            services.AddSingleton<TimeProvider>(Time);
            services.RemoveAll<ITabularQueryExecutor>();
            services.AddSingleton<ITabularQueryExecutor>(new StubTabularQueryExecutor());
            services.RemoveAll<IEmailSender>();
            services.AddSingleton<IEmailSender>(Emails);
        });
    }

    public sealed class CapturingEmailSender : IEmailSender
    {
        public List<EmailMessage> Sent { get; } = [];

        public Task SendAsync(EmailMessage message, CancellationToken cancellationToken = default)
        {
            Sent.Add(message);
            return Task.CompletedTask;
        }
    }

    private sealed class StubTabularQueryExecutor : ITabularQueryExecutor
    {
        public Task<TabularResult> ExecuteAsync(ExecutionRequest request, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new TabularResult(
                ["name", "price"],
                [new Dictionary<string, string?> { ["name"] = "Demo listing", ["price"] = "100" }],
                "Stub",
                1));
        }
    }
}
