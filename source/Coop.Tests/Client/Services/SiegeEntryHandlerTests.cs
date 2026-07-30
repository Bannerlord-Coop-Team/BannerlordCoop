using Common;
using Common.Tests.Utils;
using Common.Util;
using Coop.Core.Client.Services.SiegeEvents.Handlers;
using Coop.Core.Client.Services.SiegeEvents.Messages;
using Coop.Core.Common.Services.SiegeEvents;
using Coop.Core.Server.Services.SiegeEvents.Messages;
using Coop.Tests.Mocks;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.SiegeEvents.Interfaces;
using GameInterface.Services.SiegeEvents.Messages;
using GameInterface.Services.SiegeEvents.Validation;
using Moq;
using System.Linq;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using Xunit;

namespace Coop.Tests.Client.Services;

[Collection(nameof(ModInformationRoleCollection))]
public class SiegeEntryHandlerTests
{
    [Fact]
    public void DuplicateAndMismatchedResults_ApplyPendingEntryOnlyOnce()
    {
        var messageBroker = new TestMessageBroker();
        var network = new TestNetwork();
        var serverPeer = network.CreatePeer();
        var party = ObjectHelper.SkipConstructor<MobileParty>();
        var settlement = ObjectHelper.SkipConstructor<Settlement>();
        var objectManager = new Mock<IObjectManager>();
        var siegeEventInterface = new Mock<ISiegeEventInterface>();
        var grantStore = new SiegeInteractionGrantStore();
        const string partyId = "party";
        const string settlementId = "settlement";
        const string interactionId = "interaction";
        string resolvedPartyId = partyId;
        string resolvedSettlementId = settlementId;
        Settlement resolvedSettlement = settlement;
        objectManager
            .Setup(manager => manager.TryGetIdWithLogging(party, out resolvedPartyId))
            .Returns(true);
        objectManager
            .Setup(manager => manager.TryGetIdWithLogging(settlement, out resolvedSettlementId))
            .Returns(true);
        objectManager
            .Setup(manager => manager.TryGetObjectWithLogging(
                settlementId,
                out resolvedSettlement))
            .Returns(true);
        grantStore.RecordLocal(interactionId, partyId, settlementId);

        using var handler = new ClientSiegeEntryHandler(
            messageBroker,
            network,
            objectManager.Object,
            siegeEventInterface.Object,
            grantStore);

        messageBroker.Publish(
            this,
            new BesiegeSettlementAttempted(party, settlement));

        var requests = network
            .GetPeerMessagesFromType<NetworkRequestBesiegeSettlement>(serverPeer)
            .ToArray();
        Assert.Equal(interactionId, Assert.Single(requests).InteractionId);

        var applied = new NetworkSiegeEntryResult(
            partyId,
            settlementId,
            interactionId,
            SiegeEntryRequestType.Besiege,
            SiegeEntryOutcome.Applied,
            SiegeEntryDenialReason.None,
            SiegeEntryDisposition.Besieger,
            settlementId);
        var wrongParty = new NetworkSiegeEntryResult(
            "other-party",
            settlementId,
            interactionId,
            SiegeEntryRequestType.Besiege,
            SiegeEntryOutcome.Applied,
            SiegeEntryDenialReason.None,
            SiegeEntryDisposition.Besieger,
            settlementId);
        var wrongSettlement = new NetworkSiegeEntryResult(
            partyId,
            "other-settlement",
            interactionId,
            SiegeEntryRequestType.Besiege,
            SiegeEntryOutcome.Applied,
            SiegeEntryDenialReason.None,
            SiegeEntryDisposition.Besieger,
            settlementId);
        var wrongRequestType = new NetworkSiegeEntryResult(
            partyId,
            settlementId,
            interactionId,
            SiegeEntryRequestType.Join,
            SiegeEntryOutcome.Applied,
            SiegeEntryDenialReason.None,
            SiegeEntryDisposition.Besieger,
            settlementId);

        messageBroker.Publish(this, wrongParty);
        messageBroker.Publish(this, wrongSettlement);
        messageBroker.Publish(this, wrongRequestType);
        GameThread.Run(() => { }, blocking: true);
        siegeEventInterface.Verify(
            service => service.StartLocalPlayerSiegePreparation(),
            Times.Never);
        siegeEventInterface.Verify(
            service => service.StartLocalPlayerJoinedSiege(
                It.IsAny<Settlement>()),
            Times.Never);

        messageBroker.Publish(this, applied);
        messageBroker.Publish(this, applied);
        GameThread.Run(() => { }, blocking: true);

        siegeEventInterface.Verify(
            service => service.StartLocalPlayerSiegePreparation(),
            Times.Once);
        siegeEventInterface.Verify(
            service => service.ReconcileSiegeEntry(
                It.IsAny<SiegeEntryDisposition>(),
                It.IsAny<Settlement>()),
            Times.Never);
    }

    [Fact]
    public void MissingGrantRejection_ReconcilesOnlyTheExactPendingEntry()
    {
        var messageBroker = new TestMessageBroker();
        var network = new TestNetwork();
        var serverPeer = network.CreatePeer();
        var party = ObjectHelper.SkipConstructor<MobileParty>();
        var settlement = ObjectHelper.SkipConstructor<Settlement>();
        var canonicalSettlement = ObjectHelper.SkipConstructor<Settlement>();
        var objectManager = new Mock<IObjectManager>();
        var siegeEventInterface = new Mock<ISiegeEventInterface>();
        var grantStore = new SiegeInteractionGrantStore();
        const string partyId = "party";
        const string settlementId = "settlement";
        const string canonicalSettlementId = "canonical-settlement";
        string resolvedPartyId = partyId;
        string resolvedSettlementId = settlementId;
        Settlement resolvedCanonicalSettlement = canonicalSettlement;
        objectManager
            .Setup(manager => manager.TryGetIdWithLogging(party, out resolvedPartyId))
            .Returns(true);
        objectManager
            .Setup(manager => manager.TryGetIdWithLogging(settlement, out resolvedSettlementId))
            .Returns(true);
        objectManager
            .Setup(manager => manager.TryGetObjectWithLogging(
                canonicalSettlementId,
                out resolvedCanonicalSettlement))
            .Returns(true);

        using var handler = new ClientSiegeEntryHandler(
            messageBroker,
            network,
            objectManager.Object,
            siegeEventInterface.Object,
            grantStore);

        messageBroker.Publish(
            this,
            new BesiegeSettlementAttempted(party, settlement));

        Assert.Null(
            Assert.Single(
                network.GetPeerMessagesFromType<NetworkRequestBesiegeSettlement>(
                    serverPeer))
                .InteractionId);

        var exactResult = new NetworkSiegeEntryResult(
            partyId,
            settlementId,
            interactionId: null,
            SiegeEntryRequestType.Besiege,
            SiegeEntryOutcome.Rejected,
            SiegeEntryDenialReason.MissingInteractionGrant,
            SiegeEntryDisposition.Besieger,
            canonicalSettlementId);
        messageBroker.Publish(
            this,
            new NetworkSiegeEntryResult(
                partyId,
                settlementId,
                "unexpected-interaction",
                SiegeEntryRequestType.Besiege,
                SiegeEntryOutcome.Rejected,
                SiegeEntryDenialReason.MissingInteractionGrant,
                SiegeEntryDisposition.Besieger,
                canonicalSettlementId));
        messageBroker.Publish(
            this,
            new NetworkSiegeEntryResult(
                partyId,
                "other-settlement",
                interactionId: null,
                SiegeEntryRequestType.Besiege,
                SiegeEntryOutcome.Rejected,
                SiegeEntryDenialReason.MissingInteractionGrant,
                SiegeEntryDisposition.Besieger,
                canonicalSettlementId));
        messageBroker.Publish(this, exactResult);
        messageBroker.Publish(this, exactResult);
        GameThread.Run(() => { }, blocking: true);

        siegeEventInterface.Verify(
            service => service.ReconcileSiegeEntry(
                SiegeEntryDisposition.Besieger,
                canonicalSettlement),
            Times.Once);

        const string retryInteractionId = "retry-interaction";
        grantStore.RecordLocal(
            retryInteractionId,
            partyId,
            settlementId);
        messageBroker.Publish(
            this,
            new BesiegeSettlementAttempted(party, settlement));

        var retryRequest = network
            .GetPeerMessagesFromType<NetworkRequestBesiegeSettlement>(serverPeer)
            .Last();
        Assert.Equal(retryInteractionId, retryRequest.InteractionId);
    }

}
