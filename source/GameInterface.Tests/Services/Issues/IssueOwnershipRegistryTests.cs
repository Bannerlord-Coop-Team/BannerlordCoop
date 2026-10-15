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

    [Fact]
    public void GetAll_ReturnsEveryEntryAndDropsRemovedOnes()
    {
        var registry = new IssueOwnershipRegistry();
        registry.TrySetOwner("hero_1", "issue_1", "player-A");
        registry.TrySetOwner("hero_2", "issue_2", "player-B");

        var all = registry.GetAll();

        Assert.Equal(2, all.Length);
        Assert.Contains(all, owner => owner.IssueOwnerId == "hero_1" && owner.IssueId == "issue_1" && owner.ControllerId == "player-A");
        Assert.Contains(all, owner => owner.IssueOwnerId == "hero_2" && owner.IssueId == "issue_2" && owner.ControllerId == "player-B");

        registry.Remove("hero_1");

        var remaining = Assert.Single(registry.GetAll());
        Assert.Equal("hero_2", remaining.IssueOwnerId);
    }

    [Fact]
    public void GetAll_IsEmptyWhenNobodyClaimedAnIssue()
    {
        var registry = new IssueOwnershipRegistry();

        Assert.Empty(registry.GetAll());
    }
}
