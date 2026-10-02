using Autofac;
using Common;
using Common.Util;
using GameInterface.Policies;
using GameInterface.Services.Issues.Generic;
using GameInterface.Services.Issues.Interfaces;
using GameInterface.Services.Issues.Patches;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Players;
using GameInterface.Services.Players.Data;
using Moq;
using System;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Issues;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using Xunit;

namespace GameInterface.Tests.Services.Issues;

using Quest = HeadmanNeedsToDeliverAHerdIssueBehavior.HeadmanNeedsToDeliverAHerdIssueQuest;

[Collection(ModInformationRoleCollection.Name)]
public sealed class HeadmanHerdAuthorityTests : IDisposable
{
    private readonly bool originalRole = ModInformation.IsServer;
    private readonly IContainer container;

    public HeadmanHerdAuthorityTests()
    {
        var policy = new Mock<ISyncPolicy>();
        policy.Setup(x => x.AllowOriginal()).Returns(false);
        var builder = new ContainerBuilder();
        builder.RegisterInstance(policy.Object).As<ISyncPolicy>();
        container = builder.Build();
        ContainerProvider.SetContainer(container);
    }

    public void Dispose()
    {
        ContainerProvider.Clear();
        container.Dispose();
        ModInformation.IsServer = originalRole;
    }

    [Fact]
    public void ClientReceiveScopeDoesNotReapplyResourceConsequences()
    {
        ModInformation.IsServer = false;
        using var receive = new AllowedThread();
        using var finalization = new IssueFinalizeAuthorityGuard();

        Assert.False(HeadmanHerdReceivedConsequencePatches.Prefix());
    }

    [Fact]
    public void ClientWorldEventCannotFinalizeAQuestEvenWhileReceivingAnotherMessage()
    {
        ModInformation.IsServer = false;
        using var receive = new AllowedThread();
        var quest = ObjectHelper.SkipConstructor<Quest>();

        Assert.False(HeadmanHerdWorldEventPatches.Prefix(quest, out var scope));
        Assert.Null(scope);
    }

    [Fact]
    public void ClientCanApplyReceivedTerminalButCannotOriginateOne()
    {
        ModInformation.IsServer = false;
        var quest = ObjectHelper.SkipConstructor<Quest>();
        Assert.False(HeadmanHerdTerminalAuthorityPatches.Prefix(quest, out var rejectedScope));
        Assert.Null(rejectedScope);

        using var finalization = new IssueFinalizeAuthorityGuard();
        Assert.True(HeadmanHerdTerminalAuthorityPatches.Prefix(quest, out var receivedScope));
        Assert.Null(receivedScope);
    }

    [Fact]
    public void ServerKeepsResourceConsequencesEnabled()
    {
        ModInformation.IsServer = true;
        Assert.True(HeadmanHerdReceivedConsequencePatches.Prefix());
    }

    [Fact]
    public void MissingOwnerCannotSubstituteAnotherPlayer()
    {
        var ownership = new IssueOwnershipRegistry();
        var players = new Mock<IPlayerManager>(MockBehavior.Strict);
        var objects = new Mock<IObjectManager>(MockBehavior.Strict);
        var authority = new HeadmanHerdQuestAuthority(ownership, players.Object, objects.Object, new AwaitingAlternativeSolutionTroopsRegistry(), Mock.Of<Common.Network.INetwork>());

        Assert.False(authority.TryEnter(ObjectHelper.SkipConstructor<Hero>(), out var scope));
        Assert.Null(scope);
        players.VerifyNoOtherCalls();
        objects.VerifyNoOtherCalls();
    }

    [Fact]
    public void MissingOwnersPartyCannotUseTheCurrentParty()
    {
        var giver = ObjectHelper.SkipConstructor<Hero>();
        var hero = ObjectHelper.SkipConstructor<Hero>();
        var ownership = new IssueOwnershipRegistry();
        ownership.SetOwner(giver, "owner-A");
        var player = new Player("owner-A", "hero-A", "party-A", "clan-A", "character-A");
        var players = new Mock<IPlayerManager>();
        players.Setup(x => x.TryGetPlayer("owner-A", out player)).Returns(true);
        var objects = new Mock<IObjectManager>();
        objects.Setup(x => x.TryGetObjectWithLogging("hero-A", out hero)).Returns(true);
        MobileParty party = null;
        objects.Setup(x => x.TryGetObjectWithLogging("party-A", out party)).Returns(false);
        var authority = new HeadmanHerdQuestAuthority(ownership, players.Object, objects.Object, new AwaitingAlternativeSolutionTroopsRegistry(), Mock.Of<Common.Network.INetwork>());

        Assert.False(authority.TryEnter(giver, out var scope));
        Assert.Null(scope);
        Assert.False(IssueFinalizeAuthorityGuard.IsActive);
    }
    [Fact]
    public void NestedOwnersRouteTraitXpIndependentlyAndRestoreAfterAnException()
    {
        ModInformation.IsServer = true;
        var giverA = ObjectHelper.SkipConstructor<Hero>();
        var giverB = ObjectHelper.SkipConstructor<Hero>();
        var heroA = ObjectHelper.SkipConstructor<Hero>();
        var heroB = ObjectHelper.SkipConstructor<Hero>();
        var partyA = ObjectHelper.SkipConstructor<MobileParty>();
        var partyB = ObjectHelper.SkipConstructor<MobileParty>();
        var playerA = new Player("owner-A", "hero-A", "party-A", "clan-A", "character-A");
        var playerB = new Player("owner-B", "hero-B", "party-B", "clan-B", "character-B");
        var ownership = new IssueOwnershipRegistry();
        ownership.SetOwner(giverA, playerA.ControllerId);
        ownership.SetOwner(giverB, playerB.ControllerId);
        var players = new Mock<IPlayerManager>();
        players.Setup(x => x.TryGetPlayer("owner-A", out playerA)).Returns(true);
        players.Setup(x => x.TryGetPlayer("owner-B", out playerB)).Returns(true);
        var objects = new Mock<IObjectManager>();
        objects.Setup(x => x.TryGetObjectWithLogging("hero-A", out heroA)).Returns(true);
        objects.Setup(x => x.TryGetObjectWithLogging("hero-B", out heroB)).Returns(true);
        objects.Setup(x => x.TryGetObjectWithLogging("party-A", out partyA)).Returns(true);
        objects.Setup(x => x.TryGetObjectWithLogging("party-B", out partyB)).Returns(true);
        var authority = new HeadmanHerdQuestAuthority(ownership, players.Object, objects.Object, new AwaitingAlternativeSolutionTroopsRegistry(), Mock.Of<Common.Network.INetwork>());
        var traits = new Mock<IQuestOwnerTraitXp>(MockBehavior.Strict);
        var trait = ObjectHelper.SkipConstructor<TraitObject>();
        traits.Setup(x => x.Apply(heroA, trait, 30));
        traits.Setup(x => x.Apply(heroB, trait, -10));
        var builder = new ContainerBuilder();
        builder.RegisterInstance(traits.Object).As<IQuestOwnerTraitXp>();
        using var traitContainer = builder.Build();
        ContainerProvider.SetContainer(traitContainer);
        try
        {
            Assert.True(authority.TryEnter(giverA, out var outer));
            using (outer)
            {
                Assert.True(IssueFinalizeAuthorityGuard.IsActive);
                Assert.False(HeadmanHerdTraitXpPatch.Prefix(trait, 30));
                Assert.Throws<InvalidOperationException>((Action)(() =>
                {
                    Assert.True(authority.TryEnter(giverB, out var inner));
                    using (inner)
                    {
                        Assert.False(HeadmanHerdTraitXpPatch.Prefix(trait, -10));
                        throw new InvalidOperationException("quest callback failed");
                    }
                }));
                Assert.Same(heroA, HeadmanHerdQuestAuthority.TraitOwner);
                Assert.False(HeadmanHerdTraitXpPatch.Prefix(trait, 30));
            }
            Assert.Null(HeadmanHerdQuestAuthority.TraitOwner);
            Assert.False(IssueFinalizeAuthorityGuard.IsActive);
            Assert.True(HeadmanHerdTraitXpPatch.Prefix(trait, 1));
            traits.Verify(x => x.Apply(heroA, trait, 30), Times.Exactly(2));
            traits.Verify(x => x.Apply(heroB, trait, -10), Times.Once);
            traits.VerifyNoOtherCalls();
        }
        finally
        {
            ContainerProvider.SetContainer(container);
        }
    }

}
