using Gateway.Persistence;

namespace Gateway.Rules;

public sealed class BusinessRuleStore : IBusinessRuleStore
{
    private const string Collection = "business_rules";
    private readonly IDocumentStore _store;

    public BusinessRuleStore(IDocumentStore store)
    {
        _store = store;
    }

    public Task<BusinessRule?> GetAsync(string id, CancellationToken cancellationToken = default) =>
        _store.GetAsync<BusinessRule>(Collection, id, cancellationToken);

    public Task<IReadOnlyList<BusinessRule>> ListAsync(CancellationToken cancellationToken = default) =>
        _store.GetAllAsync<BusinessRule>(Collection, cancellationToken);

    public async Task<BusinessRule> UpsertAsync(BusinessRule rule, string updatedBy,
        CancellationToken cancellationToken = default)
    {
        var now = DateTimeOffset.UtcNow;
        var current = await _store.GetAsync<BusinessRule>(Collection, rule.Id, cancellationToken);
        var version = current is null ? 1 : current.Version + 1;

        var stored = rule
            .withId(string.IsNullOrWhiteSpace(rule.Id) ? BusinessRule.NewId() : rule.Id)
            .withVersion(version)
            with { UpdatedAt = now, UpdatedBy = updatedBy };

        await _store.UpsertAsync(Collection, stored.Id, stored, cancellationToken);
        return stored;
    }

    public Task<bool> DeleteAsync(string id, CancellationToken cancellationToken = default) =>
        _store.DeleteAsync(Collection, id, cancellationToken);
}
