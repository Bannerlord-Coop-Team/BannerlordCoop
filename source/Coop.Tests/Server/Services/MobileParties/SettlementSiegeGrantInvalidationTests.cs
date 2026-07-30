using Common;
using Common.Tests.Utils;
using Common.Util;
using Coop.Core.Client.Services.MobileParties.Messages;
using Coop.Core.Common.Services.SiegeEvents;
using Coop.Core.Server.Services.MobileParties.Handlers;
using Coop.Core.Server.Services.MobileParties.Messages;
using Coop.Tests.Mocks;
using GameInterface.Services.Kingdoms;
using GameInterface.Services.MobileParties.Messages.Behavior;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Players;
using GameInterface.Services.Players.Data;
using GameInterface.Services.Settlements.Interfaces;
using GameInterface.Services.SiegeEvents.Validation;
using Moq;
using System;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using Xunit;

namespace Coop.Tests.Server.Services.MobileParties;

[Collection(nameof(ModInformationRoleCollection))]
public class SettlementSiegeGrantInvalidationTests
{
    [Fact]
    public void RejectedSettlementRequest_PreservesTheExistingGrant()
    {
        var messageBroker = new TestMessageBroker();
        var network = new TestNetwork();
        var peer = network.CreatePeer();
        var party = ObjectHelper.SkipConstructor<MobileParty>();
        var settlement = ObjectHelper.SkipConstructor<Settlement>();
        var objectManager = new Mock<IObjectManager>();
        var playerManager = new Mock<IPlayerManager>();
        var validator = new Mock<ISiegeEntryValidator>();
        var grantStore = new SiegeInteractionGrantStore();
        const string partyId = "party";
        const string settlementId = "settlement";
        const string interactionId = "existing-interaction";
        var player = new Player("controller", "hero", partyId, "clan", "character");
        Player resolvedPlayer = player;
        MobileParty resolvedParty = party;
        Settlement resolvedSettlement = settlement;
        playerManager
            .Setup(manager => manager.TryGetPlayer(peer, out resolvedPlayer))
            .Returns(true);
        objectManager
            .Setup(manager => manager.TryGetObjectWithLogging(
                partyId,
                out resolvedParty))
            .Returns(true);
        objectManager
            .Setup(manager => manager.TryGetObjectWithLogging(
                settlementId,
                out resolvedSettlement))
            .Returns(true);
        validator
            .Setup(service => service.ValidateSettlementInteraction(
                party,
                settlement))
            .Returns(SiegeEntryValidationResult.Rejected(
                SiegeEntryDenialReason.MovementTargetMismatch,
                new SiegeEntryCanonicalState(
                    SiegeEntryDisposition.Map,
                    null)));
        grantStore.Grant(
            peer,
            interactionId,
            partyId,
            settlementId,
            presentedCamp: null);

        using var handler = new ServerSettlementExitEnterHandler(
            messageBroker,
            network,
            objectManager.Object,
            Mock.Of<ISettlementInterface>(),
            Mock.Of<IKingdomCreationSettlementTracker>(),
            playerManager.Object,
            grantStore,
            validator.Object);

        messageBroker.Publish(
            peer,
            new NetworkRequestStartSettlementEncounter(
                partyId,
                settlementId,
                "replacement-interaction"));
        GameThread.Run(() => { }, blocking: true);

        Assert.False(
            grantStore.TryConsume(
                peer,
                "replacement-interaction",
                partyId,
                settlementId,
                presentedCamp: null));
        Assert.True(
            grantStore.TryConsume(
                peer,
                interactionId,
                partyId,
                settlementId,
                presentedCamp: null));
        Assert.Single(
            network.GetPeerMessagesFromType<NetworkSettlementEncounterRejected>(
                peer));
    }

    [Fact]
    public void ApprovedSettlementRequest_ReplacesTheExistingGrant()
    {
        var messageBroker = new TestMessageBroker();
        var network = new TestNetwork();
        var peer = network.CreatePeer();
        var party = ObjectHelper.SkipConstructor<MobileParty>();
        var settlement = ObjectHelper.SkipConstructor<Settlement>();
        party._currentSettlement = settlement;
        var objectManager = new Mock<IObjectManager>();
        var playerManager = new Mock<IPlayerManager>();
        var validator = new Mock<ISiegeEntryValidator>();
        var grantStore = new SiegeInteractionGrantStore();
        const string partyId = "party";
        const string settlementId = "settlement";
        const string interactionId = "existing-interaction";
        const string replacementInteractionId = "replacement-interaction";
        var player = new Player("controller", "hero", partyId, "clan", "character");
        Player resolvedPlayer = player;
        MobileParty resolvedParty = party;
        Settlement resolvedSettlement = settlement;
        playerManager
            .Setup(manager => manager.TryGetPlayer(peer, out resolvedPlayer))
            .Returns(true);
        objectManager
            .Setup(manager => manager.TryGetObjectWithLogging(
                partyId,
                out resolvedParty))
            .Returns(true);
        objectManager
            .Setup(manager => manager.TryGetObjectWithLogging(
                settlementId,
                out resolvedSettlement))
            .Returns(true);
        validator
            .Setup(service => service.ValidateSettlementInteraction(
                party,
                settlement))
            .Returns(SiegeEntryValidationResult.Valid(
                new SiegeEntryCanonicalState(
                    SiegeEntryDisposition.Settlement,
                    settlement)));
        grantStore.Grant(
            peer,
            interactionId,
            partyId,
            settlementId,
            presentedCamp: null);

        using var handler = new ServerSettlementExitEnterHandler(
            messageBroker,
            network,
            objectManager.Object,
            Mock.Of<ISettlementInterface>(),
            Mock.Of<IKingdomCreationSettlementTracker>(),
            playerManager.Object,
            grantStore,
            validator.Object);

        messageBroker.Publish(
            peer,
            new NetworkRequestStartSettlementEncounter(
                partyId,
                settlementId,
                replacementInteractionId));
        GameThread.Run(() => { }, blocking: true);

        Assert.False(
            grantStore.TryConsume(
                peer,
                interactionId,
                partyId,
                settlementId,
                presentedCamp: null));
        Assert.True(
            grantStore.TryConsume(
                peer,
                replacementInteractionId,
                partyId,
                settlementId,
                presentedCamp: null));
        Assert.Single(
            network.GetPeerMessagesFromType<NetworkStartSettlementEncounter>(
                peer));
    }

    [Fact]
    public void EndSettlementRequest_WhenLeaveThrowsBeforeMutation_SuppressesTheLeave()
    {
        var messageBroker = new TestMessageBroker();
        var network = new TestNetwork();
        var peer = network.CreatePeer();
        var party = ObjectHelper.SkipConstructor<MobileParty>();
        var settlement = ObjectHelper.SkipConstructor<Settlement>();
        party._currentSettlement = settlement;
        var objectManager = new Mock<IObjectManager>();
        var settlementInterface = new Mock<ISettlementInterface>();
        var grantStore = new SiegeInteractionGrantStore();
        const string partyId = "party";
        const string settlementId = "settlement";
        const string interactionId = "interaction";
        MobileParty resolvedParty = party;
        objectManager
            .Setup(manager => manager.TryGetObject(
                partyId,
                out resolvedParty))
            .Returns(true);
        settlementInterface
            .Setup(service => service.PartyLeaveSettlement(party))
            .Throws<InvalidOperationException>();
        grantStore.Grant(
            peer,
            interactionId,
            partyId,
            settlementId,
            presentedCamp: null);

        using var handler = new ServerSettlementExitEnterHandler(
            messageBroker,
            network,
            objectManager.Object,
            settlementInterface.Object,
            Mock.Of<IKingdomCreationSettlementTracker>(),
            Mock.Of<IPlayerManager>(),
            grantStore,
            Mock.Of<ISiegeEntryValidator>());

        messageBroker.Publish(
            peer,
            new NetworkRequestEndSettlementEncounter(partyId));
        GameThread.Run(() => { }, blocking: true);

        settlementInterface.Verify(
            service => service.PartyLeaveSettlement(party),
            Times.Once);
        Assert.True(
            grantStore.TryConsume(
                peer,
                interactionId,
                partyId,
                settlementId,
                presentedCamp: null));
        var result = Assert.Single(
            network.GetPeerMessagesFromType<NetworkSettlementEncounterLeaveResult>(
                peer));
        Assert.Equal(SettlementEncounterLeaveOutcome.Suppressed, result.Outcome);
    }

    [Fact]
    public void EndSettlementRequest_WhenLeaveThrowsAfterMutation_CompletesTheLeave()
    {
        var messageBroker = new TestMessageBroker();
        var network = new TestNetwork();
        var peer = network.CreatePeer();
        var party = ObjectHelper.SkipConstructor<MobileParty>();
        var settlement = ObjectHelper.SkipConstructor<Settlement>();
        party._currentSettlement = settlement;
        var objectManager = new Mock<IObjectManager>();
        var settlementInterface = new Mock<ISettlementInterface>();
        var grantStore = new SiegeInteractionGrantStore();
        const string partyId = "party";
        const string settlementId = "settlement";
        const string interactionId = "interaction";
        MobileParty resolvedParty = party;
        objectManager
            .Setup(manager => manager.TryGetObject(
                partyId,
                out resolvedParty))
            .Returns(true);
        settlementInterface
            .Setup(service => service.PartyLeaveSettlement(party))
            .Callback(() => party._currentSettlement = null)
            .Throws<InvalidOperationException>();
        grantStore.Grant(
            peer,
            interactionId,
            partyId,
            settlementId,
            presentedCamp: null);

        using var handler = new ServerSettlementExitEnterHandler(
            messageBroker,
            network,
            objectManager.Object,
            settlementInterface.Object,
            Mock.Of<IKingdomCreationSettlementTracker>(),
            Mock.Of<IPlayerManager>(),
            grantStore,
            Mock.Of<ISiegeEntryValidator>());

        messageBroker.Publish(
            peer,
            new NetworkRequestEndSettlementEncounter(partyId));
        GameThread.Run(() => { }, blocking: true);

        Assert.Null(party.CurrentSettlement);
        Assert.False(
            grantStore.TryConsume(
                peer,
                interactionId,
                partyId,
                settlementId,
                presentedCamp: null));
        var result = Assert.Single(
            network.GetPeerMessagesFromType<NetworkSettlementEncounterLeaveResult>(
                peer));
        Assert.Equal(SettlementEncounterLeaveOutcome.Applied, result.Outcome);
    }

    [Fact]
    public void EndSettlementRequest_WhenPartyCannotBeResolved_CompletesTheLeave()
    {
        var messageBroker = new TestMessageBroker();
        var network = new TestNetwork();
        var peer = network.CreatePeer();
        var objectManager = new Mock<IObjectManager>();
        var grantStore = new SiegeInteractionGrantStore();
        const string partyId = "missing-party";
        const string settlementId = "settlement";
        const string interactionId = "interaction";
        grantStore.Grant(
            peer,
            interactionId,
            partyId,
            settlementId,
            presentedCamp: null);

        using var handler = new ServerSettlementExitEnterHandler(
            messageBroker,
            network,
            objectManager.Object,
            Mock.Of<ISettlementInterface>(),
            Mock.Of<IKingdomCreationSettlementTracker>(),
            Mock.Of<IPlayerManager>(),
            grantStore,
            Mock.Of<ISiegeEntryValidator>());

        messageBroker.Publish(
            peer,
            new NetworkRequestEndSettlementEncounter(partyId));
        GameThread.Run(() => { }, blocking: true);

        Assert.False(
            grantStore.TryConsume(
                peer,
                interactionId,
                partyId,
                settlementId,
                presentedCamp: null));
        var result = Assert.Single(
            network.GetPeerMessagesFromType<NetworkSettlementEncounterLeaveResult>(
                peer));
        Assert.Equal(SettlementEncounterLeaveOutcome.Applied, result.Outcome);
    }

    [Fact]
    public void EndSettlementRequest_RevokesTheInteractionGrantAfterTheLeaveReturns()
    {
        var messageBroker = new TestMessageBroker();
        var network = new TestNetwork();
        var peer = network.CreatePeer();
        var observerPeer = network.CreatePeer();
        var party = ObjectHelper.SkipConstructor<MobileParty>();
        var objectManager = new Mock<IObjectManager>();
        var settlementInterface = new Mock<ISettlementInterface>();
        var grantStore = new Mock<ISiegeInteractionGrantStore>();
        const string partyId = "party";
        MobileParty resolvedParty = party;
        int sequence = 0;
        objectManager
            .Setup(manager => manager.TryGetObject(
                partyId,
                out resolvedParty))
            .Returns(true);
        settlementInterface
            .Setup(service => service.PartyLeaveSettlement(party))
            .Callback(() =>
            {
                Assert.Equal(0, sequence++);
                messageBroker.Publish(
                    this,
                    new PartyLeaveSettlementApplied(party));
            });
        grantStore
            .Setup(store => store.Revoke(peer))
            .Callback(() => Assert.Equal(1, sequence++));

        using var handler = new ServerSettlementExitEnterHandler(
            messageBroker,
            network,
            objectManager.Object,
            settlementInterface.Object,
            Mock.Of<IKingdomCreationSettlementTracker>(),
            Mock.Of<IPlayerManager>(),
            grantStore.Object,
            Mock.Of<ISiegeEntryValidator>());

        messageBroker.Publish(
            peer,
            new NetworkRequestEndSettlementEncounter(partyId));
        GameThread.Run(() => { }, blocking: true);

        Assert.Equal(2, sequence);
        Assert.Single(
            network.GetPeerMessagesFromType<NetworkPartyLeaveSettlement>(
                observerPeer));
        Assert.Empty(
            network.GetPeerMessagesFromType<NetworkPartyLeaveSettlement>(
                peer));
    }
}
