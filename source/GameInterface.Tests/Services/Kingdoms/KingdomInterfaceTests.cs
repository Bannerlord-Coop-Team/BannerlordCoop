using Autofac;
using Common;
using Common.Messaging;
using Common.Util;
using GameInterface.Policies;
using GameInterface.Services.Kingdoms;
using GameInterface.Services.Kingdoms.Messages;
using Moq;
using System;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Election;
using TaleWorlds.Library;
using Xunit;
using CampaignKingdomDecision = TaleWorlds.CampaignSystem.Election.KingdomDecision;

namespace GameInterface.Tests.Services.Kingdoms;

[Collection(ModInformationRoleCollection.Name)]
public class KingdomInterfaceTests : IDisposable
{
    private readonly bool wasServer = ModInformation.IsServer;
    private readonly ILifetimeScope previousContainer;
    private readonly Mock<IKingdomDecisionVoteManager> voteManager = new();
    private readonly Mock<IOfflineWarProtection> offlineWarProtection = new();
    private readonly List<DecisionAdded> published = new();
    private readonly Action<MessagePayload<DecisionAdded>> capture;
    private readonly KingdomInterface kingdomInterface;
    private IContainer? container;

    public KingdomInterfaceTests()
    {
        ContainerProvider.TryGetContainer(out previousContainer);
        ModInformation.IsServer = true;
        capture = payload => published.Add(payload.What);
        MessageBroker.Instance.Subscribe(capture);
        kingdomInterface = new KingdomInterface(voteManager.Object, offlineWarProtection.Object);
    }

    public void Dispose()
    {
        MessageBroker.Instance.Unsubscribe(capture);
        ModInformation.IsServer = wasServer;
        if (previousContainer != null) ContainerProvider.SetContainer(previousContainer);
        else ContainerProvider.Clear();
        container?.Dispose();
    }

    [Fact]
    public void AddDecisionPrefix_RefusedDecision_SpendsNoInfluenceAndIsNotQueuedOrSynced()
    {
        UseSyncPolicy(allowOriginal: false);
        var kingdom = ObjectHelper.SkipConstructor<Kingdom>();
        kingdom._unresolvedDecisions = new MBList<CampaignKingdomDecision>();
        var proposerClan = ObjectHelper.SkipConstructor<Clan>();
        proposerClan._influence = 500f;
        var decision = ObjectHelper.SkipConstructor<DeclareWarDecision>();
        decision.ProposerClan = proposerClan;
        IFaction target = ObjectHelper.SkipConstructor<Kingdom>();
        offlineWarProtection.Setup(protection => protection.ShouldRefuse(decision, out target)).Returns(true);

        bool runOriginal = kingdomInterface.AddDecisionPrefix(kingdom, decision, ignoreInfluenceCost: false);

        Assert.False(runOriginal);
        Assert.Empty(published);
        Assert.Empty(kingdom._unresolvedDecisions);
        Assert.Equal(500f, proposerClan._influence);
        voteManager.VerifyNoOtherCalls();
    }

    [Fact]
    public void AddDecisionPrefix_AllowedOriginalScope_SkipsTheOfflineWarGate()
    {
        UseSyncPolicy(allowOriginal: true);
        var kingdom = ObjectHelper.SkipConstructor<Kingdom>();
        var decision = ObjectHelper.SkipConstructor<DeclareWarDecision>();

        bool runOriginal = kingdomInterface.AddDecisionPrefix(kingdom, decision, ignoreInfluenceCost: false);

        Assert.True(runOriginal);
        IFaction ignored;
        offlineWarProtection.Verify(protection => protection.ShouldRefuse(It.IsAny<CampaignKingdomDecision>(), out ignored), Times.Never);
    }

    private void UseSyncPolicy(bool allowOriginal)
    {
        var builder = new ContainerBuilder();
        builder.RegisterInstance(Mock.Of<ISyncPolicy>(policy => policy.AllowOriginal() == allowOriginal)).As<ISyncPolicy>();
        container = builder.Build();
        ContainerProvider.SetContainer(container);
    }
}
