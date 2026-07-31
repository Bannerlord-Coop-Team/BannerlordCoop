using Common;
using Common.Messaging;
using Common.Network.Coalescing;
using Common.Tests.Utils;
using Common.Util;
using Coop.Core.Client.Services.SiegeEvents.Messages;
using Coop.Core.Common.Services.SiegeEvents;
using Coop.Core.Server.Services.SiegeEvents.Handlers;
using Coop.Core.Server.Services.SiegeEvents.Messages;
using Coop.Tests.Mocks;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Players;
using GameInterface.Services.Players.Data;
using GameInterface.Services.SiegeEvents.Interfaces;
using GameInterface.Services.SiegeEvents.Validation;
using Moq;
using System;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using Xunit;

namespace Coop.Tests.Server.Services.SiegeEvents;

[Collection(nameof(ModInformationRoleCollection))]
public class ServerSiegeEntryHandlerTests
{
    [Fact]
    public void JoinRequest_WithPreviousGrant_UsesEntryValidator()
    {
        var messageBroker = new TestMessageBroker();
        var network = new TestNetwork();
        var peer = network.CreatePeer();
        var party = ObjectHelper.SkipConstructor<MobileParty>();
        var settlement = ObjectHelper.SkipConstructor<Settlement>();
        var objectManager = new Mock<IObjectManager>();
        var playerManager = new Mock<IPlayerManager>();
        var siegeEventInterface = new Mock<ISiegeEventInterface>();
        var validator = new Mock<ISiegeEntryValidator>();
        var grantStore = new SiegeInteractionGrantStore();
        const string partyId = "party";
        const string settlementId = "settlement";
        const string previousInteractionId = "previous-interaction";
        const string newestInteractionId = "newest-interaction";
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
            .Setup(service => service.ValidateEntry(
                party,
                settlement,
                SiegeEntryAction.Join))
            .Returns(SiegeEntryValidationResult.Rejected(
                SiegeEntryDenialReason.InvalidFaction,
                new SiegeEntryCanonicalState(
                    SiegeEntryDisposition.Settlement,
                    settlement)));
        grantStore.Grant(
            peer,
            previousInteractionId,
            partyId,
            settlementId,
            presentedCamp: null);
        grantStore.Grant(
            peer,
            newestInteractionId,
            partyId,
            settlementId,
            presentedCamp: null);

        using var handler = new ServerSiegeEntryHandler(
            messageBroker,
            network,
            objectManager.Object,
            playerManager.Object,
            new SendCoalescer(),
            siegeEventInterface.Object,
            grantStore,
            validator.Object);

        messageBroker.Publish(
            peer,
            new NetworkRequestJoinSiegeCamp(
                partyId,
                settlementId,
                previousInteractionId));
        GameThread.Run(() => { }, blocking: true);

        var result = Assert.Single(
            network.GetPeerMessagesFromType<NetworkSiegeEntryResult>(peer));
        Assert.Equal(SiegeEntryDenialReason.InvalidFaction, result.Reason);
        Assert.True(
            grantStore.TryConsume(
                peer,
                newestInteractionId,
                partyId,
                settlementId,
                presentedCamp: null));
        siegeEventInterface.Verify(
            service => service.JoinSiegeCamp(
                It.IsAny<MobileParty>(),
                It.IsAny<Settlement>()),
            Times.Never);
    }

    [Fact]
    public void BesiegeRequest_WhenAuthoritativeActionThrows_SendsTerminalResult()
    {
        var messageBroker = new TestMessageBroker();
        var network = new TestNetwork();
        var peer = network.CreatePeer();
        var party = ObjectHelper.SkipConstructor<MobileParty>();
        var settlement = ObjectHelper.SkipConstructor<Settlement>();
        var objectManager = new Mock<IObjectManager>();
        var playerManager = new Mock<IPlayerManager>();
        var siegeEventInterface = new Mock<ISiegeEventInterface>();
        var validator = new Mock<ISiegeEntryValidator>();
        var grantStore = new SiegeInteractionGrantStore();
        const string partyId = "party";
        const string settlementId = "settlement";
        const string interactionId = "interaction";
        var player = new Player("controller", "hero", partyId, "clan", "character");
        Player resolvedPlayer = player;
        MobileParty resolvedParty = party;
        Settlement resolvedSettlement = settlement;
        int sequence = 0;
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
            .Setup(service => service.ValidateEntry(
                party,
                settlement,
                SiegeEntryAction.Besiege))
            .Returns(SiegeEntryValidationResult.Valid(
                new SiegeEntryCanonicalState(SiegeEntryDisposition.Map, null)));
        siegeEventInterface
            .Setup(service => service.StartSiegeEvent(party, settlement))
            .Callback(() =>
            {
                Assert.Equal(0, sequence++);
                throw new InvalidOperationException("fixture failure");
            });
        validator
            .Setup(service => service.GetCanonicalState(party))
            .Callback(() => Assert.Equal(1, sequence++))
            .Returns(new SiegeEntryCanonicalState(
                SiegeEntryDisposition.Map,
                null));
        grantStore.Grant(
            peer,
            interactionId,
            partyId,
            settlementId,
            presentedCamp: null);

        using var handler = new ServerSiegeEntryHandler(
            messageBroker,
            network,
            objectManager.Object,
            playerManager.Object,
            new SendCoalescer(),
            siegeEventInterface.Object,
            grantStore,
            validator.Object);

        messageBroker.Publish(
            peer,
            new NetworkRequestBesiegeSettlement(
                partyId,
                settlementId,
                interactionId));
        GameThread.Run(() => { }, blocking: true);

        Assert.Equal(2, sequence);
        var result = Assert.Single(
            network.GetPeerMessagesFromType<NetworkSiegeEntryResult>(peer));
        Assert.Equal(partyId, result.PartyId);
        Assert.Equal(settlementId, result.RequestedSettlementId);
        Assert.Equal(interactionId, result.InteractionId);
        Assert.Equal(SiegeEntryRequestType.Besiege, result.RequestType);
        Assert.Equal(SiegeEntryOutcome.Rejected, result.Outcome);
        Assert.Equal(SiegeEntryDenialReason.ActionFailed, result.Reason);
        Assert.Equal(SiegeEntryDisposition.Map, result.Disposition);
    }
}
