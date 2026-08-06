using Autofac;
using Common.Util;
using GameInterface.Services.Entity;
using GameInterface.Services.Issues.Generic;
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
        IssueOwnershipRegistry.ClearAll();
    }

    public void Dispose()
    {
        IssueOwnershipRegistry.ClearAll();
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
        IssueOwnershipRegistry.SetOwner(issueGiver, "player-A");

        SetLocalControllerId("player-A");
        Assert.True(IssueOwnershipRegistry.IsLocalPeerOwner(issueGiver));

        SetLocalControllerId("player-B");
        Assert.False(IssueOwnershipRegistry.IsLocalPeerOwner(issueGiver));
    }

    [Fact]
    public void IsLocalPeerOwner_FalseWhenNoOwnerHasEverBeenRecorded()
    {
        var issueGiver = NewHero();
        SetLocalControllerId("player-A");

        Assert.False(IssueOwnershipRegistry.IsLocalPeerOwner(issueGiver));
    }

    [Fact]
    public void IsLocalPeerOwner_FalseWhenNoControllerIdProviderCanBeResolved()
    {
        var issueGiver = NewHero();
        IssueOwnershipRegistry.SetOwner(issueGiver, "player-A");
        ContainerProvider.Clear();

        Assert.False(IssueOwnershipRegistry.IsLocalPeerOwner(issueGiver));
    }

    [Fact]
    public void SetOwner_IgnoresNullOrEmptyControllerId()
    {
        var issueGiver = NewHero();

        IssueOwnershipRegistry.SetOwner(issueGiver, null);
        IssueOwnershipRegistry.SetOwner(issueGiver, string.Empty);

        Assert.False(IssueOwnershipRegistry.TryGetOwnerControllerId(issueGiver, out _));
    }

    [Fact]
    public void SetOwner_OverwritesAPreviouslyRecordedOwnerForTheSameHero()
    {
        var issueGiver = NewHero();
        IssueOwnershipRegistry.SetOwner(issueGiver, "player-A");
        IssueOwnershipRegistry.SetOwner(issueGiver, "player-B");

        Assert.True(IssueOwnershipRegistry.TryGetOwnerControllerId(issueGiver, out var controllerId));
        Assert.Equal("player-B", controllerId);
    }

    [Fact]
    public void Clear_RemovesOnlyThatHeroSoALaterUnrelatedIssueForTheSameHeroStartsClean()
    {
        var issueGiver = NewHero();
        var otherIssueGiver = NewHero();
        IssueOwnershipRegistry.SetOwner(issueGiver, "player-A");
        IssueOwnershipRegistry.SetOwner(otherIssueGiver, "player-B");

        IssueOwnershipRegistry.Clear(issueGiver);

        Assert.False(IssueOwnershipRegistry.TryGetOwnerControllerId(issueGiver, out _));
        Assert.True(IssueOwnershipRegistry.TryGetOwnerControllerId(otherIssueGiver, out var otherControllerId));
        Assert.Equal("player-B", otherControllerId);
    }

    [Fact]
    public void Snapshot_RoundTripsThroughClearAllExactlyAsThePersistencePatchRelies()
    {
        var heroA = NewHero();
        var heroB = NewHero();
        IssueOwnershipRegistry.SetOwner(heroA, "player-A");
        IssueOwnershipRegistry.SetOwner(heroB, "player-B");

        var snapshot = IssueOwnershipRegistry.Snapshot().ToList();
        Assert.Equal(2, snapshot.Count);
        Assert.Contains(snapshot, kvp => kvp.Key == heroA && kvp.Value == "player-A");
        Assert.Contains(snapshot, kvp => kvp.Key == heroB && kvp.Value == "player-B");

        IssueOwnershipRegistry.ClearAll();
        Assert.Empty(IssueOwnershipRegistry.Snapshot());

        foreach (var kvp in snapshot)
        {
            IssueOwnershipRegistry.SetOwner(kvp.Key, kvp.Value);
        }

        Assert.True(IssueOwnershipRegistry.TryGetOwnerControllerId(heroA, out var idA));
        Assert.Equal("player-A", idA);
        Assert.True(IssueOwnershipRegistry.TryGetOwnerControllerId(heroB, out var idB));
        Assert.Equal("player-B", idB);
    }
}
