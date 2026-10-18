namespace Gateway.Rules;

public interface IBusinessRuleStore
{
    Task<BusinessRule?> GetAsync(string id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<BusinessRule>> ListAsync(CancellationToken cancellationToken = default);

    Task<BusinessRule> UpsertAsync(BusinessRule rule, string updatedBy,
        CancellationToken cancellationToken = default);

    Task<bool> DeleteAsync(string id, CancellationToken cancellationToken = default);
}
