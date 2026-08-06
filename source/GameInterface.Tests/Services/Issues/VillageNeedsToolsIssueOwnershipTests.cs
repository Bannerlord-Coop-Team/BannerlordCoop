using Autofac;
using Common.Util;
using GameInterface.Services.Entity;
using GameInterface.Services.Issues.Interfaces;
using GameInterface.Tests;
using Moq;
using System;
using System.Linq;
using TaleWorlds.CampaignSystem;
using Xunit;

namespace GameInterface.Tests.Services.Issues;

/// <summary>
/// Exercises the real <see cref="VillageNeedsToolsIssueOwnership"/> registry directly (not a
/// reimplementation of its logic) - this is the "ownership-gate mechanism" the harmony patches in
/// GameInterface/Services/Issues/Patches lean on. <see cref="ContainerProvider"/> is process-wide static
/// state, so every test here runs in <see cref="ModInformationRoleCollection"/> to avoid a parallel test
/// elsewhere observing a container swapped out from under it.
/// </summary>
[Collection(ModInformationRoleCollection.Name)]
public class VillageNeedsToolsIssueOwnershipTests : IDisposable
{
    public VillageNeedsToolsIssueOwnershipTests()
    {
        // The registry is static/process-wide too - start every test from a clean slate regardless of
        // what a previous test (or a previous run's crash) left behind.
        VillageNeedsToolsIssueOwnership.ClearAll();
    }

    public void Dispose()
    {
        VillageNeedsToolsIssueOwnership.ClearAll();
        ContainerProvider.Clear();
    }

    private static Hero NewHero() => ObjectHelper.SkipConstructor<Hero>();

    /// <summary>
    /// Installs a container whose <see cref="IControllerIdProvider"/> reports <paramref name="controllerId"/>
    /// as "this machine's own player" - exactly what <see cref="VillageNeedsToolsIssueOwnership.IsLocalPeerOwner"/>
    /// resolves via <see cref="ContainerProvider"/> in production.
    /// </summary>
    private static void SetLocalControllerId(string controllerId)
    {
        var provider = new Mock<IControllerIdProvider>();
        provider.SetupGet(p => p.ControllerId).Returns(controllerId);

        var builder = new ContainerBuilder();
        builder.RegisterInstance(provider.Object).As<IControllerIdProvider>();
        ContainerProvider.SetContainer(builder.Build());
    }

    [Fact]
    public void IsLocalPeerOwner_TrueOnlyOnTheMachineWhoseControllerIdMatchesTheRecordedOwner()
    {
        var issueGiver = NewHero();
        VillageNeedsToolsIssueOwnership.SetOwner(issueGiver, "player-A");

        SetLocalControllerId("player-A");
        Assert.True(VillageNeedsToolsIssueOwnership.IsLocalPeerOwner(issueGiver));

        // A different peer (e.g. the losing side of an accept race, or a dedicated server with no
        // local player) must never read itself as the owner just because SOMEONE owns the issue.
        SetLocalControllerId("player-B");
        Assert.False(VillageNeedsToolsIssueOwnership.IsLocalPeerOwner(issueGiver));
    }

    [Fact]
    public void IsLocalPeerOwner_FalseWhenNoOwnerHasEverBeenRecorded()
    {
        var issueGiver = NewHero();
        SetLocalControllerId("player-A");

        Assert.False(VillageNeedsToolsIssueOwnership.IsLocalPeerOwner(issueGiver));
    }

    [Fact]
    public void IsLocalPeerOwner_FalseWhenNoControllerIdProviderCanBeResolved()
    {
        // A dedicated server with no local player is exactly this case: no IControllerIdProvider
        // registration to resolve at all.
        var issueGiver = NewHero();
        VillageNeedsToolsIssueOwnership.SetOwner(issueGiver, "player-A");
        ContainerProvider.Clear();

        Assert.False(VillageNeedsToolsIssueOwnership.IsLocalPeerOwner(issueGiver));
    }

    [Fact]
    public void SetOwner_IgnoresNullOrEmptyControllerId()
    {
        var issueGiver = NewHero();

        VillageNeedsToolsIssueOwnership.SetOwner(issueGiver, null);
        VillageNeedsToolsIssueOwnership.SetOwner(issueGiver, string.Empty);

        Assert.False(VillageNeedsToolsIssueOwnership.TryGetOwnerControllerId(issueGiver, out _));
    }

    [Fact]
    public void SetOwner_OverwritesAPreviouslyRecordedOwnerForTheSameHero()
    {
        // Models a same-issue double-accept race resolving: the server's arbitration confirms a winner
        // AFTER a different (losing) ControllerId may have raced in first.
        var issueGiver = NewHero();
        VillageNeedsToolsIssueOwnership.SetOwner(issueGiver, "player-A");
        VillageNeedsToolsIssueOwnership.SetOwner(issueGiver, "player-B");

        Assert.True(VillageNeedsToolsIssueOwnership.TryGetOwnerControllerId(issueGiver, out var controllerId));
        Assert.Equal("player-B", controllerId);
    }

    [Fact]
    public void Clear_RemovesOnlyThatHeroSoALaterUnrelatedIssueForTheSameHeroStartsClean()
    {
        var issueGiver = NewHero();
        var otherIssueGiver = NewHero();
        VillageNeedsToolsIssueOwnership.SetOwner(issueGiver, "player-A");
        VillageNeedsToolsIssueOwnership.SetOwner(otherIssueGiver, "player-B");

        VillageNeedsToolsIssueOwnership.Clear(issueGiver);

        Assert.False(VillageNeedsToolsIssueOwnership.TryGetOwnerControllerId(issueGiver, out _));
        // The unrelated hero's own entry must survive - Clear is keyed per-hero, not a blanket wipe.
        Assert.True(VillageNeedsToolsIssueOwnership.TryGetOwnerControllerId(otherIssueGiver, out var otherControllerId));
        Assert.Equal("player-B", otherControllerId);
    }

    [Fact]
    public void Snapshot_RoundTripsThroughClearAllExactlyAsThePersistencePatchRelies()
    {
        // This is exactly the save/load shape VillageNeedsToolsIssueOwnershipPersistencePatches uses:
        // snapshot everything, ClearAll, then re-populate from the snapshot.
        var heroA = NewHero();
        var heroB = NewHero();
        VillageNeedsToolsIssueOwnership.SetOwner(heroA, "player-A");
        VillageNeedsToolsIssueOwnership.SetOwner(heroB, "player-B");

        var snapshot = VillageNeedsToolsIssueOwnership.Snapshot().ToList();
        Assert.Equal(2, snapshot.Count);
        Assert.Contains(snapshot, kvp => kvp.Key == heroA && kvp.Value == "player-A");
        Assert.Contains(snapshot, kvp => kvp.Key == heroB && kvp.Value == "player-B");

        VillageNeedsToolsIssueOwnership.ClearAll();
        Assert.Empty(VillageNeedsToolsIssueOwnership.Snapshot());

        foreach (var kvp in snapshot)
        {
            VillageNeedsToolsIssueOwnership.SetOwner(kvp.Key, kvp.Value);
        }

        Assert.True(VillageNeedsToolsIssueOwnership.TryGetOwnerControllerId(heroA, out var idA));
        Assert.Equal("player-A", idA);
        Assert.True(VillageNeedsToolsIssueOwnership.TryGetOwnerControllerId(heroB, out var idB));
        Assert.Equal("player-B", idB);
    }
}
