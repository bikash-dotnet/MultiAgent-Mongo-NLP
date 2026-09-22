using Gateway.Conversations;
using Gateway.Governance;
using Gateway.Nlp;
using Gateway.Nlp.Orchestrator;
using Gateway.Nlp.Router;
using Gateway.Reports;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Gateway.Tests.Conversations;

public class ConversationDiTests
{
    [Fact]
    public void AddGatewayNlp_registers_the_conversation_services()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IHostEnvironment>(new StubEnvironment());
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());

        services.AddGatewayNlp(new ConfigurationBuilder().Build());

        services.AddSingleton<INlpOrchestrator, StubNlpOrchestrator>();

        using var provider = services.BuildServiceProvider();
        Assert.NotNull(provider.GetService<IApprovalFlagStore>());
        Assert.NotNull(provider.GetService<IConversationStore>());
        Assert.NotNull(provider.GetService<INotificationSender>());
        Assert.NotNull(provider.GetService<IColumnCatalog>());
        Assert.NotNull(provider.GetService<ConversationOrchestrator>());
    }

    private sealed class StubNlpOrchestrator : INlpOrchestrator
    {
        public Task<NlpRouteResult> OrchestrateAsync(
            string utterance,
            string sessionId = "anonymous",
            CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();
    }

    private sealed class StubEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = Environments.Development;
        public string ApplicationName { get; set; } = "Gateway.Tests";
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public Microsoft.Extensions.FileProviders.IFileProvider ContentRootFileProvider { get; set; } =
            new Microsoft.Extensions.FileProviders.NullFileProvider();
    }
}
