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
    private readonly IIssueOwnershipRegistry registry = new IssueOwnershipRegistry();

    public void Dispose()
    {
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
        registry.SetOwner(issueGiver, "player-A");

        SetLocalControllerId("player-A");
        Assert.True(registry.IsLocalPeerOwner(issueGiver));

        SetLocalControllerId("player-B");
        Assert.False(registry.IsLocalPeerOwner(issueGiver));
    }

    [Fact]
    public void IsLocalPeerOwner_FalseWhenNoOwnerHasEverBeenRecorded()
    {
        var issueGiver = NewHero();
        SetLocalControllerId("player-A");

        Assert.False(registry.IsLocalPeerOwner(issueGiver));
    }

    [Fact]
    public void IsLocalPeerOwner_FalseWhenNoControllerIdProviderCanBeResolved()
    {
        var issueGiver = NewHero();
        registry.SetOwner(issueGiver, "player-A");
        ContainerProvider.Clear();

        Assert.False(registry.IsLocalPeerOwner(issueGiver));
    }

    [Fact]
    public void SetOwner_IgnoresNullOrEmptyControllerId()
    {
        var issueGiver = NewHero();

        registry.SetOwner(issueGiver, null);
        registry.SetOwner(issueGiver, string.Empty);

        Assert.False(registry.TryGetOwnerControllerId(issueGiver, out _));
    }

    [Fact]
    public void SetOwner_OverwritesAPreviouslyRecordedOwnerForTheSameHero()
    {
        var issueGiver = NewHero();
        registry.SetOwner(issueGiver, "player-A");
        registry.SetOwner(issueGiver, "player-B");

        Assert.True(registry.TryGetOwnerControllerId(issueGiver, out var controllerId));
        Assert.Equal("player-B", controllerId);
    }

    [Fact]
    public void Clear_RemovesOnlyThatHeroSoALaterUnrelatedIssueForTheSameHeroStartsClean()
    {
        var issueGiver = NewHero();
        var otherIssueGiver = NewHero();
        registry.SetOwner(issueGiver, "player-A");
        registry.SetOwner(otherIssueGiver, "player-B");

        registry.Clear(issueGiver);

        Assert.False(registry.TryGetOwnerControllerId(issueGiver, out _));
        Assert.True(registry.TryGetOwnerControllerId(otherIssueGiver, out var otherControllerId));
        Assert.Equal("player-B", otherControllerId);
    }

    [Fact]
    public void Snapshot_RoundTripsThroughClearAllExactlyAsThePersistencePatchRelies()
    {
        var heroA = NewHero();
        var heroB = NewHero();
        registry.SetOwner(heroA, "player-A");
        registry.SetOwner(heroB, "player-B");

        var snapshot = registry.Snapshot().ToList();
        Assert.Equal(2, snapshot.Count);
        Assert.Contains(snapshot, kvp => kvp.Key == heroA && kvp.Value == "player-A");
        Assert.Contains(snapshot, kvp => kvp.Key == heroB && kvp.Value == "player-B");

        registry.ClearAll();
        Assert.Empty(registry.Snapshot());

        foreach (var kvp in snapshot)
        {
            registry.SetOwner(kvp.Key, kvp.Value);
        }

        Assert.True(registry.TryGetOwnerControllerId(heroA, out var idA));
        Assert.Equal("player-A", idA);
        Assert.True(registry.TryGetOwnerControllerId(heroB, out var idB));
        Assert.Equal("player-B", idB);
    }
}
