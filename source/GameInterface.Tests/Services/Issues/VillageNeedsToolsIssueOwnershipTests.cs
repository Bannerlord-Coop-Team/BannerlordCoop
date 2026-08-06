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

[Collection(ModInformationRoleCollection.Name)]
public class VillageNeedsToolsIssueOwnershipTests : IDisposable
{
    public VillageNeedsToolsIssueOwnershipTests()
    {
        VillageNeedsToolsIssueOwnership.ClearAll();
    }

    public void Dispose()
    {
        VillageNeedsToolsIssueOwnership.ClearAll();
        ContainerProvider.Clear();
    }

    private static Hero NewHero() => ObjectHelper.SkipConstructor<Hero>();

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
        Assert.True(VillageNeedsToolsIssueOwnership.TryGetOwnerControllerId(otherIssueGiver, out var otherControllerId));
        Assert.Equal("player-B", otherControllerId);
    }

    [Fact]
    public void Snapshot_RoundTripsThroughClearAllExactlyAsThePersistencePatchRelies()
    {
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
