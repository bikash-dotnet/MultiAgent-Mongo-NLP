using Gateway.Governance;

namespace Gateway.Tests.Governance;

public class RoleNormalizerTests
{
    [Theory]
    [InlineData("Data Owner / Admin", "DataOwner")]
    [InlineData("Data Owner / Admin", "Admin")]
    [InlineData("dataowner", "DataOwner")]
    public void Matches_owner_roles(string claim, string owner)
    {
        Assert.True(RoleNormalizer.Matches(claim, owner));
    }

    [Theory]
    [InlineData("Team Lead", "DataOwner")]
    [InlineData("Business Analyst", "Admin")]
    [InlineData("Engineering Manager", "Director")]
    [InlineData("", "Admin")]
    public void Rejects_non_matching_roles(string claim, string owner)
    {
        Assert.False(RoleNormalizer.Matches(claim, owner));
    }

    [Fact]
    public void Normalize_strips_spaces_and_punctuation()
    {
        Assert.Equal("dataowneradmin", RoleNormalizer.Normalize("Data Owner / Admin"));
    }
}
