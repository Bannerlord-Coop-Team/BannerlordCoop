using Common;
using Common.Tests.Utils;
using Common.Util;
using Coop.Core.Client.Services.MobileParties.Handlers;
using Coop.Core.Client.Services.MobileParties.Messages;
using Coop.Core.Common.Services.SiegeEvents;
using Coop.Tests.Mocks;
using GameInterface.Services.MobileParties.Messages.Behavior;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Settlements.Interfaces;
using Moq;
using System;
using TaleWorlds.CampaignSystem.Party;
using Xunit;

namespace Coop.Tests.Client.Services;

[Collection(nameof(ModInformationRoleCollection))]
public class SettlementSiegeGrantTests
{
    [Fact]
    public void ReplicatedPartyLeave_ClearsOwningGrantAfterTheLeaveApplies()
    {
        var messageBroker = new TestMessageBroker();
        var party = ObjectHelper.SkipConstructor<MobileParty>();
        var objectManager = new Mock<IObjectManager>();
        var settlementInterface = new Mock<ISettlementInterface>();
        var grantStore = new SiegeInteractionGrantStore();
        const string partyId = "party";
        MobileParty resolvedParty = party;
        objectManager
            .Setup(manager => manager.TryGetObjectWithLogging(
                partyId,
                out resolvedParty))
            .Returns(true);
        grantStore.RecordLocal("interaction", partyId, "settlement");

        using var handler = new ClientSettlementExitEnterHandler(
            messageBroker,
            new TestNetwork(),
            objectManager.Object,
            settlementInterface.Object,
            grantStore);

        messageBroker.Publish(this, new NetworkPartyLeaveSettlement(partyId));
        GameThread.Run(() => { }, blocking: true);

        settlementInterface.Verify(
            service => service.PartyLeaveSettlement(party),
            Times.Once);
        Assert.False(
            grantStore.TryConsumeLocal(
                partyId,
                "settlement",
                out _));
    }

    [Fact]
    public void ReplicatedPartyLeave_ThatCannotApply_PreservesOwningGrant()
    {
        var messageBroker = new TestMessageBroker();
        var objectManager = new Mock<IObjectManager>();
        var settlementInterface = new Mock<ISettlementInterface>();
        var grantStore = new SiegeInteractionGrantStore();
        const string partyId = "party";
        grantStore.RecordLocal("interaction", partyId, "settlement");

        using var handler = new ClientSettlementExitEnterHandler(
            messageBroker,
            new TestNetwork(),
            objectManager.Object,
            settlementInterface.Object,
            grantStore);

        messageBroker.Publish(this, new NetworkPartyLeaveSettlement(partyId));
        GameThread.Run(() => { }, blocking: true);

        settlementInterface.Verify(
            service => service.PartyLeaveSettlement(
                It.IsAny<MobileParty>()),
            Times.Never);
        Assert.True(
            grantStore.TryConsumeLocal(
                partyId,
                "settlement",
                out var interactionId));
        Assert.Equal("interaction", interactionId);
    }

    [Fact]
    public void ReplicatedPartyLeave_WhenApplyThrows_PreservesOwningGrant()
    {
        var messageBroker = new TestMessageBroker();
        var party = ObjectHelper.SkipConstructor<MobileParty>();
        var objectManager = new Mock<IObjectManager>();
        var settlementInterface = new Mock<ISettlementInterface>();
        var grantStore = new SiegeInteractionGrantStore();
        const string partyId = "party";
        MobileParty resolvedParty = party;
        objectManager
            .Setup(manager => manager.TryGetObjectWithLogging(
                partyId,
                out resolvedParty))
            .Returns(true);
        settlementInterface
            .Setup(service => service.PartyLeaveSettlement(party))
            .Throws<InvalidOperationException>();
        grantStore.RecordLocal("interaction", partyId, "settlement");

        using var handler = new ClientSettlementExitEnterHandler(
            messageBroker,
            new TestNetwork(),
            objectManager.Object,
            settlementInterface.Object,
            grantStore);

        messageBroker.Publish(this, new NetworkPartyLeaveSettlement(partyId));
        GameThread.Run(() => { }, blocking: true);

        Assert.True(
            grantStore.TryConsumeLocal(
                partyId,
                "settlement",
                out var interactionId));
        Assert.Equal("interaction", interactionId);
    }

    [Fact]
    public void AppliedEncounterLeave_WhenTeardownThrows_PreservesOwningGrant()
    {
        var messageBroker = new TestMessageBroker();
        var party = ObjectHelper.SkipConstructor<MobileParty>();
        var objectManager = new Mock<IObjectManager>();
        var settlementInterface = new Mock<ISettlementInterface>();
        var grantStore = new SiegeInteractionGrantStore();
        const string partyId = "party";
        string resolvedPartyId = partyId;
        string mainPartyId = partyId;
        objectManager
            .Setup(manager => manager.TryGetIdWithLogging(
                party,
                out resolvedPartyId))
            .Returns(true);
        objectManager
            .Setup(manager => manager.TryGetId(
                It.IsAny<object>(),
                out mainPartyId))
            .Returns(true);
        settlementInterface
            .Setup(service => service.EndSettlementEncounter())
            .Throws<InvalidOperationException>();
        grantStore.RecordLocal("interaction", partyId, "settlement");

        using var handler = new ClientSettlementExitEnterHandler(
            messageBroker,
            new TestNetwork(),
            objectManager.Object,
            settlementInterface.Object,
            grantStore);

        messageBroker.Publish(this, new EndSettlementEncounterAttempted(party));
        messageBroker.Publish(
            this,
            new NetworkSettlementEncounterLeaveResult(
                partyId,
                SettlementEncounterLeaveOutcome.Applied));
        GameThread.Run(() => { }, blocking: true);

        Assert.True(
            grantStore.TryConsumeLocal(
                partyId,
                "settlement",
                out var interactionId));
        Assert.Equal("interaction", interactionId);
    }

    [Fact]
    public void SuppressedEncounterLeave_PreservesOwningGrant()
    {
        var messageBroker = new TestMessageBroker();
        var party = ObjectHelper.SkipConstructor<MobileParty>();
        var objectManager = new Mock<IObjectManager>();
        var grantStore = new SiegeInteractionGrantStore();
        const string partyId = "party";
        string resolvedPartyId = partyId;
        objectManager
            .Setup(manager => manager.TryGetIdWithLogging(
                party,
                out resolvedPartyId))
            .Returns(true);
        grantStore.RecordLocal("interaction", partyId, "settlement");

        using var handler = new ClientSettlementExitEnterHandler(
            messageBroker,
            new TestNetwork(),
            objectManager.Object,
            Mock.Of<ISettlementInterface>(),
            grantStore);

        messageBroker.Publish(
            this,
            new EndSettlementEncounterAttempted(party));
        messageBroker.Publish(
            this,
            new NetworkSettlementEncounterLeaveResult(
                partyId,
                SettlementEncounterLeaveOutcome.Suppressed));
        GameThread.Run(() => { }, blocking: true);

        Assert.True(
            grantStore.TryConsumeLocal(
                partyId,
                "settlement",
                out var interactionId));
        Assert.Equal("interaction", interactionId);
    }

    [Fact]
    public void RevokeParty_RemovesEveryRemoteGrantForThatPartyOnly()
    {
        var network = new TestNetwork();
        var firstPeer = network.CreatePeer();
        var secondPeer = network.CreatePeer();
        var otherPartyPeer = network.CreatePeer();
        var grantStore = new SiegeInteractionGrantStore();
        grantStore.Grant(firstPeer, "first", "party", "settlement-one", null);
        grantStore.Grant(secondPeer, "second", "party", "settlement-two", null);
        grantStore.Grant(otherPartyPeer, "other", "other-party", "settlement", null);

        grantStore.RevokeParty("party");

        Assert.False(
            grantStore.TryConsume(
                firstPeer,
                "first",
                "party",
                "settlement-one",
                null));
        Assert.False(
            grantStore.TryConsume(
                secondPeer,
                "second",
                "party",
                "settlement-two",
                null));
        Assert.True(
            grantStore.TryConsume(
                otherPartyPeer,
                "other",
                "other-party",
                "settlement",
                null));
    }
}
