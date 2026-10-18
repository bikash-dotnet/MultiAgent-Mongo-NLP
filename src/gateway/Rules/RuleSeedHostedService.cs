using Gateway.Conversations;
using Gateway.Persistence;

namespace Gateway.Rules;

public sealed class RuleSeedHostedService : IHostedService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IColumnCatalog _columnCatalog;
    private readonly IConfiguration _configuration;

    public RuleSeedHostedService(
        IServiceScopeFactory scopeFactory,
        IColumnCatalog columnCatalog,
        IConfiguration configuration)
    {
        _scopeFactory = scopeFactory;
        _columnCatalog = columnCatalog;
        _configuration = configuration;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var ruleStore = scope.ServiceProvider.GetRequiredService<IBusinessRuleStore>();
        var columnStore = scope.ServiceProvider.GetRequiredService<IColumnDefinitionStore>();
        return SeedAsync(ruleStore, columnStore, cancellationToken);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    private static async Task SeedAsync(
        IBusinessRuleStore ruleStore,
        IColumnDefinitionStore columnStore,
        CancellationToken cancellationToken)
    {
        var existingRules = await ruleStore.ListAsync(cancellationToken);
        if (existingRules.Count > 0)
        {
            return;
        }

        var now = DateTimeOffset.UtcNow;
        var rules = new List<BusinessRule>
        {
            new(
                BusinessRule.NewId(),
                "Default result limit",
                "Apply the configured default result limit when the user does not specify one.",
                RuleScope.Global,
                null,
                10,
                true,
                new RuleCondition("*", "equals", ["*"], "all"),
                new RuleAction(RuleActions.SetDefault, new Dictionary<string, string>
                {
                    ["key"] = "limit",
                    ["value"] = "10"
                }),
                1, now, "system"),
            new(
                BusinessRule.NewId(),
                "Deny destructive operators",
                "Reject write/admin operators as a defense-in-depth safety rule.",
                RuleScope.Global,
                null,
                20,
                true,
                new RuleCondition("*", "equals", ["*"], "all"),
                new RuleAction(RuleActions.DenyOperator, new Dictionary<string, string>
                {
                    ["operator"] = "delete"
                }),
                1, now, "system"),
            new(
                BusinessRule.NewId(),
                "Deny drop operator",
                "Reject the drop operator as a defense-in-depth safety rule.",
                RuleScope.Global,
                null,
                21,
                true,
                new RuleCondition("*", "equals", ["*"], "all"),
                new RuleAction(RuleActions.DenyOperator, new Dictionary<string, string>
                {
                    ["operator"] = "drop"
                }),
                1, now, "system")
        };

        foreach (var rule in rules)
        {
            await ruleStore.UpsertAsync(rule, "system", cancellationToken);
        }

        var existingColumns = await columnStore.ListAsync(cancellationToken);
        if (existingColumns.Count > 0)
        {
            return;
        }

        var order = 0;
        foreach (var field in ColumnCatalog.Standard)
        {
            var def = new ColumnDefinition(
                ColumnDefinition.NewId(),
                field,
                field,
                ColumnDataType.String,
                null,
                order++,
                true,
                true,
                false,
                null,
                1, now, "system");
            await columnStore.UpsertAsync(def, "system", cancellationToken);
        }
    }
}
