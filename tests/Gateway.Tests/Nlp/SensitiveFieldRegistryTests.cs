using Gateway.Nlp.Guardrails;

namespace Gateway.Tests.Nlp;

public class SensitiveFieldRegistryTests
{
    private static readonly InMemorySensitiveFieldRegistry Registry = new(
    [
        new SensitiveFieldFlag("address.location.coordinates", true, true, ["DataOwner", "Admin"]),
        new SensitiveFieldFlag("host.host_verifications", true, true, ["DataOwner", "Admin"]),
        new SensitiveFieldFlag("host.host_identity_verified", true, false, ["DataOwner", "Admin"])
    ]);

    [Fact]
    public void Exact_path_matches()
    {
        Assert.True(Registry.TryGet("address.location.coordinates", out var flag));
        Assert.True(flag.RequiresApproval);
    }

    [Theory]
    [InlineData("address.location.coordinates.lat")]
    [InlineData("address")]
    public void Related_paths_match(string fieldPath)
    {
        var matched = Registry.Match([fieldPath]);

        Assert.Single(matched);
        Assert.Equal("address.location.coordinates", matched[0].Path);
    }

    [Fact]
    public void Non_sensitive_path_does_not_match()
    {
        Assert.Empty(Registry.Match(["address.market"]));
        Assert.False(Registry.TryGet("address.market", out _));
    }

    [Fact]
    public void Loads_seed_registry_from_asset_directory()
    {
        var directory = Path.Combine(TestPaths.RepoRoot(), "src", "gateway", "Nlp", "Assets", "Schema");
        var registry = InMemorySensitiveFieldRegistry.LoadFromDirectory(directory);

        Assert.Equal(3, registry.All.Count);
        Assert.Contains(registry.All, f => f.Path == "address.location.coordinates" && f.RequiresApproval);
        Assert.Contains(registry.All, f => f.Path == "host.host_verifications" && f.RequiresApproval);
    }
}
