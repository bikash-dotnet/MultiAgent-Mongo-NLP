using System.Text.Json;

namespace Gateway.Configuration;

public interface ISettingsStore
{
    Task<SettingsDocument?> GetAsync(string sectionKey, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<SettingsDocument>> ListAsync(CancellationToken cancellationToken = default);

    Task<SettingsDocument> UpsertAsync(string sectionKey, JsonElement values, string updatedBy,
        CancellationToken cancellationToken = default);

    Task<bool> DeleteAsync(string sectionKey, CancellationToken cancellationToken = default);

    Task ResetAsync(CancellationToken cancellationToken = default);
}
