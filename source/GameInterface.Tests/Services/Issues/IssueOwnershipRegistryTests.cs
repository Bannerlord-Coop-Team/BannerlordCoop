using GameInterface.Services.Issues.Framework.Registries;
using Xunit;

namespace GameInterface.Tests.Services.Issues;

public class IssueOwnershipRegistryTests
{
    [Fact]
    public void TrySetOwner_FirstControllerWins()
    {
        var registry = new IssueOwnershipRegistry();

        Assert.True(registry.TrySetOwner("hero", "issue_1", "player-A"));
        Assert.False(registry.TrySetOwner("hero", "issue_1", "player-B"));

        Assert.True(registry.TryGetOwner("hero", "issue_1", out var controllerId));
        Assert.Equal("player-A", controllerId);
    }

    [Fact]
    public void TrySetOwner_SameControllerClaimingAgainStillOwnsTheIssue()
    {
        var registry = new IssueOwnershipRegistry();

        Assert.True(registry.TrySetOwner("hero", "issue_1", "player-A"));
        Assert.True(registry.TrySetOwner("hero", "issue_1", "player-A"));
    }

    [Fact]
    public void TrySetOwner_ANewIssueOfTheSameHeroReplacesTheOldEntry()
    {
        var registry = new IssueOwnershipRegistry();

        Assert.True(registry.TrySetOwner("hero", "issue_1", "player-A"));
        Assert.True(registry.TrySetOwner("hero", "issue_2", "player-B"));

        Assert.False(registry.TryGetOwner("hero", "issue_1", out _));
        Assert.True(registry.TryGetOwner("hero", "issue_2", out var controllerId));
        Assert.Equal("player-B", controllerId);
    }

    [Fact]
    public void TryGetOwner_FailsForAnIssueNobodyClaimed()
    {
        var registry = new IssueOwnershipRegistry();

        Assert.False(registry.TryGetOwner("hero", "issue_1", out var controllerId));
        Assert.Null(controllerId);
    }

    [Fact]
    public void Remove_ForgetsTheOwner()
    {
        var registry = new IssueOwnershipRegistry();
        registry.TrySetOwner("hero", "issue_1", "player-A");

        registry.Remove("hero");

        Assert.False(registry.TryGetOwner("hero", "issue_1", out _));
        Assert.True(registry.TrySetOwner("hero", "issue_1", "player-B"));
    }
}
