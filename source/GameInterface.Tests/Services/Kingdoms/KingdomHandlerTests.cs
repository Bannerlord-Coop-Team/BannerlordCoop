using Common;
using Common.Messaging;
using Common.Network;
using Common.Util;
using GameInterface.Services.Heroes.Extensions;
using GameInterface.Services.Kingdoms;
using GameInterface.Services.Kingdoms.Handlers;
using GameInterface.Services.Kingdoms.Messages;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Players;
using GameInterface.Services.Players.Data;
using GameInterface.Tests.Bootstrap;
using LiteNetLib;
using Moq;
using System;
using System.Collections.Generic;
using System.Reflection;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Election;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Library;
using Xunit;
using static TaleWorlds.MountAndBlade.MPOnSpawnPerkEffectBase;
using CampaignKingdomDecision = TaleWorlds.CampaignSystem.Election.KingdomDecision;

namespace GameInterface.Tests.Services.Kingdoms;

public class KingdomHandlerTests
{
    [Fact]
    public void TryGetCulture_UsesObjectManager()
    {
        var culture = ObjectHelper.SkipConstructor<CultureObject>();
        var objectManager = new Mock<IObjectManager>();
        CultureObject resolvedCulture = culture;
        objectManager.Setup(manager => manager.TryGetObject("culture-id", out resolvedCulture)).Returns(true);
        var handler = CreateHandler(objectManager.Object);

        bool result = TryGetCulture(handler, "culture-id", out CultureObject actualCulture);

        Assert.True(result);
        Assert.Same(culture, actualCulture);
        objectManager.Verify(manager => manager.TryGetObject("culture-id", out resolvedCulture), Times.Once);
    }

    [Fact]
    public void TryGetCulture_ReturnsFalseWhenObjectManagerCannotResolveCulture()
    {
        var objectManager = new Mock<IObjectManager>();
        CultureObject missingCulture = null!;
        objectManager.Setup(manager => manager.TryGetObject("culture-id", out missingCulture)).Returns(false);
        var handler = CreateHandler(objectManager.Object);

        bool result = TryGetCulture(handler, "culture-id", out CultureObject culture);

        Assert.False(result);
        Assert.Null(culture);
        objectManager.Verify(manager => manager.TryGetObject("culture-id", out missingCulture), Times.Once);
    }

    [Fact]
    public void CanChangeKingdomName_NullClan_ReturnsFalse()
    {
        var kingdom = ObjectHelper.SkipConstructor<Kingdom>();
        bool result = KingdomHandler.CanChangeKingdomName(
            null!,
            kingdom,
            "New Kingdom",
            out string reason);

        Assert.False(result);
        Assert.Equal("clan was null", reason);
    }

    [Fact]
    public void CanChangeKingdomName_NullKingdom_ReturnsFalse()
    {
        var clan = ObjectHelper.SkipConstructor<Clan>();
        bool result = KingdomHandler.CanChangeKingdomName(
            clan,
            null!,
            "New Kingdom",
            out string reason);

        Assert.False(result);
        Assert.Equal("kingdom was null", reason);
    }

    [Fact]
    public void CanChangeKingdomName_ClanIsNotMember_ReturnsFalse()
    {
        var clan = ObjectHelper.SkipConstructor<Clan>();
        var kingdom = ObjectHelper.SkipConstructor<Kingdom>();

        bool result = KingdomHandler.CanChangeKingdomName(
            clan,
            kingdom,
            "New Kingdom",
            out string reason);

        Assert.False(result);
        Assert.Equal("clan is not a member of the kingdom", reason);
    }

    [Fact]
    public void CanChangeKingdomName_ClanIsNotRuler_ReturnsFalse()
    {
        var clan = ObjectHelper.SkipConstructor<Clan>();
        var otherClan = ObjectHelper.SkipConstructor<Clan>();
        var kingdom = ObjectHelper.SkipConstructor<Kingdom>();

        clan._kingdom = kingdom;
        kingdom._rulingClan = otherClan;

        bool result = KingdomHandler.CanChangeKingdomName(
            clan,
            kingdom,
            "New Kingdom",
            out string reason);

        Assert.False(result);
        Assert.Equal("clan is not the ruling clan of the kingdom", reason);
    }

    [Fact]
    public void CanChangeKingdomName_RulingClanWithName_ReturnsTrue()
    {
        var clan = ObjectHelper.SkipConstructor<Clan>();
        var kingdom = ObjectHelper.SkipConstructor<Kingdom>();

        clan._kingdom = kingdom;
        kingdom._rulingClan = clan;

        bool result = KingdomHandler.CanChangeKingdomName(
            clan,
            kingdom,
            "New Kingdom",
            out string reason);

        Assert.True(result);
        Assert.Null(reason);
    }

    [Fact]
    public void RemoveDecision_ClearsStateBeforeClosingAndRemovingDecision()
    {
        var kingdom = ObjectHelper.SkipConstructor<Kingdom>();
        var decision = ObjectHelper.SkipConstructor<DeclareWarDecision>();
        kingdom._unresolvedDecisions = new MBList<CampaignKingdomDecision> { decision };
        var objectManager = new Mock<IObjectManager>();
        Kingdom resolvedKingdom = kingdom;
        objectManager.Setup(manager => manager.TryGetObject("kingdom-id", out resolvedKingdom)).Returns(true);
        var voteManager = new Mock<IKingdomDecisionVoteManager>();
        var kingdomInterface = new Mock<IKingdomInterface>();
        var calls = new List<string>();
        voteManager.Setup(manager => manager.ClearDecisionState("kingdom-id", 0))
            .Callback(() => calls.Add("clear"));
        voteManager.Setup(manager => manager.CloseDecision("kingdom-id", 0))
            .Callback(() => calls.Add("close"));
        kingdomInterface.Setup(manager => manager.RemoveDecision(kingdom, decision))
            .Callback(() => calls.Add("remove"));
        Action<MessagePayload<RemoveDecision>> handler = CreateRemoveDecisionHandler(
            objectManager.Object,
            voteManager.Object,
            kingdomInterface.Object);
        RunWithBoundGameThread(() =>
        {
            handler(new MessagePayload<RemoveDecision>(this, new RemoveDecision("kingdom-id", 0)));

            Assert.Equal(new[] { "clear", "close", "remove" }, calls);
            voteManager.Verify(manager => manager.ClearDecisionState("kingdom-id", 0), Times.Once);
            voteManager.Verify(manager => manager.CloseDecision("kingdom-id", 0), Times.Once);
            kingdomInterface.Verify(manager => manager.RemoveDecision(kingdom, decision), Times.Once);
        });
    }

    [Fact]
    public void RemoveDecision_MissingKingdom_ClearsStateOnce()
    {
        var objectManager = new Mock<IObjectManager>();
        Kingdom missingKingdom = null!;
        objectManager.Setup(manager => manager.TryGetObject("kingdom-id", out missingKingdom)).Returns(false);
        var voteManager = new Mock<IKingdomDecisionVoteManager>();
        var kingdomInterface = new Mock<IKingdomInterface>();
        Action<MessagePayload<RemoveDecision>> handler = CreateRemoveDecisionHandler(
            objectManager.Object,
            voteManager.Object,
            kingdomInterface.Object);

        handler(new MessagePayload<RemoveDecision>(this, new RemoveDecision("kingdom-id", 0)));

        voteManager.Verify(manager => manager.ClearDecisionState("kingdom-id", 0), Times.Once);
        kingdomInterface.Verify(
            manager => manager.RemoveDecision(It.IsAny<Kingdom>(), It.IsAny<CampaignKingdomDecision>()),
            Times.Never);
    }

    [Fact]
    public void RemoveDecision_NullDecisionList_ClearsStateOnce()
    {
        var kingdom = ObjectHelper.SkipConstructor<Kingdom>();
        kingdom._unresolvedDecisions = null;
        var objectManager = new Mock<IObjectManager>();
        Kingdom resolvedKingdom = kingdom;
        objectManager.Setup(manager => manager.TryGetObject("kingdom-id", out resolvedKingdom)).Returns(true);
        var voteManager = new Mock<IKingdomDecisionVoteManager>();
        var kingdomInterface = new Mock<IKingdomInterface>();
        Action<MessagePayload<RemoveDecision>> handler = CreateRemoveDecisionHandler(
            objectManager.Object,
            voteManager.Object,
            kingdomInterface.Object);

        handler(new MessagePayload<RemoveDecision>(this, new RemoveDecision("kingdom-id", 0)));

        voteManager.Verify(manager => manager.ClearDecisionState("kingdom-id", 0), Times.Once);
        kingdomInterface.Verify(
            manager => manager.RemoveDecision(It.IsAny<Kingdom>(), It.IsAny<CampaignKingdomDecision>()),
            Times.Never);
    }

    [Fact]
    public void RemoveDecision_OutOfRange_ClearsStateOnce()
    {
        var kingdom = ObjectHelper.SkipConstructor<Kingdom>();
        kingdom._unresolvedDecisions = new MBList<CampaignKingdomDecision>();
        var objectManager = new Mock<IObjectManager>();
        Kingdom resolvedKingdom = kingdom;
        objectManager.Setup(manager => manager.TryGetObject("kingdom-id", out resolvedKingdom)).Returns(true);
        var voteManager = new Mock<IKingdomDecisionVoteManager>();
        var kingdomInterface = new Mock<IKingdomInterface>();
        Action<MessagePayload<RemoveDecision>> handler = CreateRemoveDecisionHandler(
            objectManager.Object,
            voteManager.Object,
            kingdomInterface.Object);

        handler(new MessagePayload<RemoveDecision>(this, new RemoveDecision("kingdom-id", 1)));

        voteManager.Verify(manager => manager.ClearDecisionState("kingdom-id", 1), Times.Once);
        kingdomInterface.Verify(
            manager => manager.RemoveDecision(It.IsAny<Kingdom>(), It.IsAny<CampaignKingdomDecision>()),
            Times.Never);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public void CanChangeKingdomName_EmptyName_ReturnsFalse(string? requestedName)
    {
        var clan = ObjectHelper.SkipConstructor<Clan>();
        var kingdom = ObjectHelper.SkipConstructor<Kingdom>();

        clan._kingdom = kingdom;
        kingdom._rulingClan = clan;

        bool result = KingdomHandler.CanChangeKingdomName(
            clan,
            kingdom,
            requestedName!,
            out string reason);

        Assert.False(result);
        Assert.Equal("kingdom name was empty", reason);
    }

    [Fact]
    public void NetworkGiftSettlementOwnership_UnregisteredSender_IsIgnored()
    {
        var objectManager = new Mock<IObjectManager>();
        var playerManager = new Mock<IPlayerManager>();
        var handler = CreateNetworkGiftSettlementOwnershipHandler(objectManager.Object, playerManager.Object);

        handler(new MessagePayload<NetworkGiftSettlementOwnership>(
            null!,
            new NetworkGiftSettlementOwnership("settlement-id", "receiver-clan-id")));

        Player ignoredPlayer = null!;
        playerManager.Verify(
            manager => manager.TryGetPlayer(It.IsAny<NetPeer>(), out ignoredPlayer),
            Times.Never);
        objectManager.VerifyNoOtherCalls();
    }

    [Fact]
    public void NetworkGiftSettlementOwnership_MissingSettlement_IsIgnored()
    {
        var peer = ObjectHelper.SkipConstructor<NetPeer>();
        var player = ObjectHelper.SkipConstructor<Player>();

        var objectManager = new Mock<IObjectManager>();
        var playerManager = new Mock<IPlayerManager>();
        playerManager
            .Setup(manager => manager.TryGetPlayer(peer, out player))
            .Returns(true);
        SetupGiftObjects(objectManager, null!, null!, null!, null!);

        var handler = CreateNetworkGiftSettlementOwnershipHandler(objectManager.Object, playerManager.Object);
        RunWithBoundGameThread(() =>
        {
            handler(new MessagePayload<NetworkGiftSettlementOwnership>(
                peer,
                new NetworkGiftSettlementOwnership("settlement-id", "receiver-clan-id")));
            DrainGameThread();

            Settlement missingSettlement = null!;
            objectManager.Verify(
                manager => manager.TryGetObjectWithLogging("settlement-id", out missingSettlement),
                Times.Once);
            objectManager.VerifyNoOtherCalls();
        });
    }
    [Fact]
    public void NetworkGiftSettlementOwnership_MissingReceiverClan_IsIgnored()
    {
        var peer = ObjectHelper.SkipConstructor<NetPeer>();
        var player = ObjectHelper.SkipConstructor<Player>();
        var settlement = ObjectHelper.SkipConstructor<Settlement>();
        var objectManager = new Mock<IObjectManager>();
        var playerManager = new Mock<IPlayerManager>();
        playerManager
            .Setup(manager => manager.TryGetPlayer(peer, out player))
            .Returns(true);
        SetupGiftObjects(objectManager, settlement, null!, null!, null!);

        var handler = CreateNetworkGiftSettlementOwnershipHandler(objectManager.Object, playerManager.Object);
        RunWithBoundGameThread(() =>
        {
            handler(new MessagePayload<NetworkGiftSettlementOwnership>(
                peer,
                new NetworkGiftSettlementOwnership("settlement-id", "receiver-clan-id")));
            DrainGameThread();

            Settlement retrievedSettlement = null!;
            Clan missingReceiverClan = null!;
            objectManager.Verify(
                manager => manager.TryGetObjectWithLogging("settlement-id", out retrievedSettlement),
                Times.Once);
            objectManager.Verify(
                manager => manager.TryGetObjectWithLogging("receiver-clan-id", out missingReceiverClan),
                Times.Once);
            objectManager.VerifyNoOtherCalls();
        });
    }

    [Fact]
    public void NetworkGiftSettlementOwnership_MissingSenderClan_IsIgnored()
    {
        var peer = ObjectHelper.SkipConstructor<NetPeer>();
        var player = ObjectHelper.SkipConstructor<Player>();
        SetField(player, "ClanId", "sender-clan-id");
        SetField(player, "HeroId", "sender-hero-id");
        var settlement = ObjectHelper.SkipConstructor<Settlement>();
        var receiverClan = ObjectHelper.SkipConstructor<Clan>();

        var objectManager = new Mock<IObjectManager>();
        var playerManager = new Mock<IPlayerManager>();
        playerManager
            .Setup(manager => manager.TryGetPlayer(peer, out player))
            .Returns(true);
        SetupGiftObjects(objectManager, settlement, receiverClan, null!, null!);

        var handler = CreateNetworkGiftSettlementOwnershipHandler(objectManager.Object, playerManager.Object);
        RunWithBoundGameThread(() =>
        {
            handler(new MessagePayload<NetworkGiftSettlementOwnership>(
            peer,
            new NetworkGiftSettlementOwnership("settlement-id", "receiver-clan-id")));
            DrainGameThread();

            Clan retrievedReceiverClan = null!;
            Settlement retrievedSettlement = null!;
            Clan missingSenderClan = null!;
            objectManager.Verify(
                manager => manager.TryGetObjectWithLogging("settlement-id", out retrievedSettlement),
                Times.Once);
            objectManager.Verify(
                manager => manager.TryGetObjectWithLogging("receiver-clan-id", out retrievedReceiverClan),
                Times.Once);
            objectManager.Verify(
                manager => manager.TryGetObjectWithLogging("sender-clan-id", out missingSenderClan),
                Times.Once);
            objectManager.VerifyNoOtherCalls();
        });
    }

    [Fact]
    public void NetworkGiftSettlementOwnership_MissingSenderHero_IsIgnored()
    {
        var peer = ObjectHelper.SkipConstructor<NetPeer>();
        var player = ObjectHelper.SkipConstructor<Player>();
        SetField(player, "ClanId", "sender-clan-id");
        SetField(player, "HeroId", "sender-hero-id");
        var settlement = ObjectHelper.SkipConstructor<Settlement>();
        var receiverClan = ObjectHelper.SkipConstructor<Clan>();
        var senderClan = ObjectHelper.SkipConstructor<Clan>();

        var objectManager = new Mock<IObjectManager>();
        var playerManager = new Mock<IPlayerManager>();
        playerManager
            .Setup(manager => manager.TryGetPlayer(peer, out player))
            .Returns(true);
        SetupGiftObjects(objectManager, settlement, receiverClan, senderClan, null!);

        var handler = CreateNetworkGiftSettlementOwnershipHandler(objectManager.Object, playerManager.Object);
        RunWithBoundGameThread(() =>
        {
            handler(new MessagePayload<NetworkGiftSettlementOwnership>(
            peer,
            new NetworkGiftSettlementOwnership("settlement-id", "receiver-clan-id")));
            DrainGameThread();

            Clan retrievedReceiverClan = null!;
            Settlement retrievedSettlement = null!;
            Clan missingSenderClan = null!;
            Hero retrievedSenderHero = null!;
            objectManager.Verify(
                manager => manager.TryGetObjectWithLogging("settlement-id", out retrievedSettlement),
                Times.Once);
            objectManager.Verify(
                manager => manager.TryGetObjectWithLogging("receiver-clan-id", out retrievedReceiverClan),
                Times.Once);
            objectManager.Verify(
                manager => manager.TryGetObjectWithLogging("sender-clan-id", out missingSenderClan),
                Times.Once);
            objectManager.Verify(
                manager => manager.TryGetObjectWithLogging("sender-hero-id", out retrievedSenderHero),
                Times.Once);
            objectManager.VerifyNoOtherCalls();
        });
    }

    [Fact]
    public void NetworkGiftSettlementOwnership_SenderIsNotRulingClan_IsIgnored()
    {
        var peer = ObjectHelper.SkipConstructor<NetPeer>();
        var player = ObjectHelper.SkipConstructor<Player>();
        SetField(player, "ClanId", "sender-clan-id");
        SetField(player, "HeroId", "sender-hero-id");
        var settlement = ObjectHelper.SkipConstructor<Settlement>();
        var senderClan = ObjectHelper.SkipConstructor<Clan>();
        var receiverClan = ObjectHelper.SkipConstructor<Clan>();
        var kingdom = ObjectHelper.SkipConstructor<Kingdom>();
        var senderHero = ObjectHelper.SkipConstructor<Hero>();
        kingdom._rulingClan = receiverClan;
        senderClan._kingdom = kingdom;
        senderClan._leader = senderHero;
        receiverClan._kingdom = kingdom;

        var objectManager = new Mock<IObjectManager>();
        var playerManager = new Mock<IPlayerManager>();
        playerManager
            .Setup(manager => manager.TryGetPlayer(peer, out player))
            .Returns(true);
        SetupGiftObjects(objectManager, settlement, receiverClan, senderClan, senderHero);

        var handler = CreateNetworkGiftSettlementOwnershipHandler(objectManager.Object, playerManager.Object);
        RunWithBoundGameThread(() =>
        {
            handler(new MessagePayload<NetworkGiftSettlementOwnership>(
            peer,
            new NetworkGiftSettlementOwnership("settlement-id", "receiver-clan-id")));
            DrainGameThread();

            Clan retrievedReceiverClan = null!;
            Clan retrievedSenderClan = null!;
            Settlement retrievedSettlement = null!;
            Hero retrievedSenderHero = null!;
            objectManager.Verify(
                manager => manager.TryGetObjectWithLogging("settlement-id", out retrievedSettlement),
                Times.Once);
            objectManager.Verify(
                manager => manager.TryGetObjectWithLogging("receiver-clan-id", out retrievedReceiverClan),
                Times.Once);
            objectManager.Verify(
                manager => manager.TryGetObjectWithLogging("sender-clan-id", out retrievedSenderClan),
                Times.Once);
            objectManager.Verify(
                manager => manager.TryGetObjectWithLogging("sender-hero-id", out retrievedSenderHero),
                Times.Once);
            objectManager.VerifyNoOtherCalls();
        });
    }

    [Fact]
    public void NetworkGiftSettlementOwnership_SenderIsNotClanLeader_IsIgnored()
    {
        var peer = ObjectHelper.SkipConstructor<NetPeer>();
        var player = ObjectHelper.SkipConstructor<Player>();
        SetField(player, "ClanId", "sender-clan-id");
        SetField(player, "HeroId", "sender-hero-id");
        var settlement = ObjectHelper.SkipConstructor<Settlement>();
        var senderClan = ObjectHelper.SkipConstructor<Clan>();
        var senderHero = ObjectHelper.SkipConstructor<Hero>();
        var receiverClan = ObjectHelper.SkipConstructor<Clan>();
        var kingdom = ObjectHelper.SkipConstructor<Kingdom>();
        kingdom._rulingClan = receiverClan;
        senderClan._kingdom = kingdom;
        senderClan._leader = senderHero;
        receiverClan._kingdom = kingdom;

        var objectManager = new Mock<IObjectManager>();
        var playerManager = new Mock<IPlayerManager>();
        playerManager
            .Setup(manager => manager.TryGetPlayer(peer, out player))
            .Returns(true);
        SetupGiftObjects(objectManager, settlement, receiverClan, senderClan, senderHero);

        var handler = CreateNetworkGiftSettlementOwnershipHandler(objectManager.Object, playerManager.Object);
        RunWithBoundGameThread(() =>
        {
            handler(new MessagePayload<NetworkGiftSettlementOwnership>(
            peer,
            new NetworkGiftSettlementOwnership("settlement-id", "receiver-clan-id")));
            DrainGameThread();

            Clan retrievedReceiverClan = null!;
            Clan retrievedSenderClan = null!;
            Settlement retrievedSettlement = null!;
            Hero retrievedSenderHero = null!;
            objectManager.Verify(
                manager => manager.TryGetObjectWithLogging("settlement-id", out retrievedSettlement),
                Times.Once);
            objectManager.Verify(
                manager => manager.TryGetObjectWithLogging("receiver-clan-id", out retrievedReceiverClan),
                Times.Once);
            objectManager.Verify(
                manager => manager.TryGetObjectWithLogging("sender-clan-id", out retrievedSenderClan),
                Times.Once);
            objectManager.Verify(
                manager => manager.TryGetObjectWithLogging("sender-hero-id", out retrievedSenderHero),
                Times.Once);
            objectManager.VerifyNoOtherCalls();
        });
    }

    [Fact]
    public void NetworkGiftSettlementOwnership_ReceiverIsUnderMercenaryService_IsIgnored()
    {
        var peer = ObjectHelper.SkipConstructor<NetPeer>();
        var player = ObjectHelper.SkipConstructor<Player>();
        SetField(player, "ClanId", "sender-clan-id");
        SetField(player, "HeroId", "sender-hero-id");
        var settlement = ObjectHelper.SkipConstructor<Settlement>();
        var senderClan = ObjectHelper.SkipConstructor<Clan>();
        var senderHero = ObjectHelper.SkipConstructor<Hero>();
        var receiverClan = ObjectHelper.SkipConstructor<Clan>();
        var kingdom = ObjectHelper.SkipConstructor<Kingdom>();

        senderClan._kingdom = kingdom;
        senderClan._leader = senderHero;
        receiverClan._kingdom = kingdom;
        kingdom._rulingClan = senderClan;
        receiverClan.IsUnderMercenaryService = true;

        var objectManager = new Mock<IObjectManager>();
        var playerManager = new Mock<IPlayerManager>();
        playerManager
            .Setup(manager => manager.TryGetPlayer(peer, out player))
            .Returns(true);
        SetupGiftObjects(objectManager, settlement, receiverClan, senderClan, senderHero);

        var handler = CreateNetworkGiftSettlementOwnershipHandler(objectManager.Object, playerManager.Object);
        RunWithBoundGameThread(() =>
        {
            handler(new MessagePayload<NetworkGiftSettlementOwnership>(
            peer,
            new NetworkGiftSettlementOwnership("settlement-id", "receiver-clan-id")));
            DrainGameThread();

            Clan retrievedReceiverClan = null!;
            Clan retrievedSenderClan = null!;
            Settlement retrievedSettlement = null!;
            Hero retrievedSenderHero = null!;
            objectManager.Verify(
                manager => manager.TryGetObjectWithLogging("settlement-id", out retrievedSettlement),
                Times.Once);
            objectManager.Verify(
                manager => manager.TryGetObjectWithLogging("receiver-clan-id", out retrievedReceiverClan),
                Times.Once);
            objectManager.Verify(
                manager => manager.TryGetObjectWithLogging("sender-clan-id", out retrievedSenderClan),
                Times.Once);
            objectManager.Verify(
                manager => manager.TryGetObjectWithLogging("sender-hero-id", out retrievedSenderHero),
                Times.Once);
            objectManager.VerifyNoOtherCalls();
        });
    }

    [Fact]
    public void NetworkGiftSettlementOwnership_ReceiverIsInDifferentKingdom_IsIgnored()
    {
        var peer = ObjectHelper.SkipConstructor<NetPeer>();
        var player = ObjectHelper.SkipConstructor<Player>();
        SetField(player, "ClanId", "sender-clan-id");
        SetField(player, "HeroId", "sender-hero-id");
        var settlement = ObjectHelper.SkipConstructor<Settlement>();
        var senderClan = ObjectHelper.SkipConstructor<Clan>();
        var senderHero = ObjectHelper.SkipConstructor<Hero>();
        var receiverClan = ObjectHelper.SkipConstructor<Clan>();
        var senderKingdom = ObjectHelper.SkipConstructor<Kingdom>();
        var receiverKingdom = ObjectHelper.SkipConstructor<Kingdom>();

        senderClan._kingdom = senderKingdom;
        senderClan._leader = senderHero;
        receiverClan._kingdom = receiverKingdom;
        senderKingdom._rulingClan = senderClan;

        var objectManager = new Mock<IObjectManager>();
        var playerManager = new Mock<IPlayerManager>();

        playerManager
            .Setup(manager => manager.TryGetPlayer(peer, out player))
            .Returns(true);
        SetupGiftObjects(objectManager, settlement, receiverClan, senderClan, senderHero);

        var handler = CreateNetworkGiftSettlementOwnershipHandler(objectManager.Object, playerManager.Object);
        RunWithBoundGameThread(() =>
        {
            handler(new MessagePayload<NetworkGiftSettlementOwnership>(
            peer,
            new NetworkGiftSettlementOwnership("settlement-id", "receiver-clan-id")));
            DrainGameThread();

            Clan retrievedReceiverClan = null!;
            Clan retrievedSenderClan = null!;
            Hero retrievedSenderHero = null!;
            Settlement retrievedSettlement = null!;
            objectManager.Verify(
                manager => manager.TryGetObjectWithLogging("settlement-id", out retrievedSettlement),
                Times.Once);
            objectManager.Verify(
                manager => manager.TryGetObjectWithLogging("receiver-clan-id", out retrievedReceiverClan),
                Times.Once);
            objectManager.Verify(
                manager => manager.TryGetObjectWithLogging("sender-clan-id", out retrievedSenderClan),
                Times.Once);
            objectManager.Verify(
                manager => manager.TryGetObjectWithLogging("sender-hero-id", out retrievedSenderHero),
                Times.Once);
            objectManager.VerifyNoOtherCalls();
        });
    }

    [Fact]
    public void NetworkGiftSettlementOwnership_ReceiverIsSenderClan_IsIgnored()
    {
        var peer = ObjectHelper.SkipConstructor<NetPeer>();
        var player = ObjectHelper.SkipConstructor<Player>();
        SetField(player, "ClanId", "sender-clan-id");
        SetField(player, "HeroId", "sender-hero-id");
        var settlement = ObjectHelper.SkipConstructor<Settlement>();
        var senderClan = ObjectHelper.SkipConstructor<Clan>();
        var senderHero = ObjectHelper.SkipConstructor<Hero>();
        var kingdom = ObjectHelper.SkipConstructor<Kingdom>();

        senderClan._kingdom = kingdom;
        senderClan._leader = senderHero;
        kingdom._rulingClan = senderClan;

        var objectManager = new Mock<IObjectManager>();
        var playerManager = new Mock<IPlayerManager>();

        playerManager
            .Setup(manager => manager.TryGetPlayer(peer, out player))
            .Returns(true);
        SetupGiftObjects(objectManager, settlement, senderClan, senderClan, senderHero);

        var handler = CreateNetworkGiftSettlementOwnershipHandler(objectManager.Object, playerManager.Object);

        RunWithBoundGameThread(() =>
        {
            handler(new MessagePayload<NetworkGiftSettlementOwnership>(
            peer,
            new NetworkGiftSettlementOwnership("settlement-id", "sender-clan-id")));
            DrainGameThread();

            Clan retrievedSenderClan = null!;
            Settlement retrievedSettlement = null!;
            Hero retrievedSenderHero = null!;
            objectManager.Verify(
                manager => manager.TryGetObjectWithLogging("settlement-id", out retrievedSettlement),
                Times.Once);
            objectManager.Verify(
                manager => manager.TryGetObjectWithLogging("sender-clan-id", out retrievedSenderClan),
                Times.Exactly(2));
            objectManager.Verify(
                manager => manager.TryGetObjectWithLogging("sender-hero-id", out retrievedSenderHero),
                Times.Once);
            objectManager.VerifyNoOtherCalls();
        });
    }

    [Fact]
    public void NetworkGiftSettlementOwnership_SettlementIsNotFortification_IsIgnored()
    {
        var peer = ObjectHelper.SkipConstructor<NetPeer>();
        var player = ObjectHelper.SkipConstructor<Player>();
        SetField(player, "ClanId", "sender-clan-id");
        SetField(player, "HeroId", "sender-hero-id");
        var settlement = ObjectHelper.SkipConstructor<Settlement>();
        settlement.Village = ObjectHelper.SkipConstructor<Village>();
        var senderClan = ObjectHelper.SkipConstructor<Clan>();
        var senderHero = ObjectHelper.SkipConstructor<Hero>();
        var receiverClan = ObjectHelper.SkipConstructor<Clan>();
        var kingdom = ObjectHelper.SkipConstructor<Kingdom>();

        senderClan._kingdom = kingdom;
        senderClan._leader = senderHero;
        receiverClan._kingdom = kingdom;
        kingdom._rulingClan = senderClan;

        var objectManager = new Mock<IObjectManager>();
        var playerManager = new Mock<IPlayerManager>();
        playerManager
            .Setup(manager => manager.TryGetPlayer(peer, out player))
            .Returns(true);
        SetupGiftObjects(objectManager, settlement, receiverClan, senderClan, senderHero);

        var handler = CreateNetworkGiftSettlementOwnershipHandler(objectManager.Object, playerManager.Object);

        RunWithBoundGameThread(() =>
        {
            handler(new MessagePayload<NetworkGiftSettlementOwnership>(
            peer,
            new NetworkGiftSettlementOwnership("settlement-id", "receiver-clan-id")));
            DrainGameThread();

            Clan retrievedReceiverClan = null!;
            Clan retrievedSenderClan = null!;
            Settlement retrievedSettlement = null!;
            Hero retrievedSenderHero = null!;
            objectManager.Verify(
                manager => manager.TryGetObjectWithLogging("settlement-id", out retrievedSettlement),
                Times.Once);
            objectManager.Verify(
                manager => manager.TryGetObjectWithLogging("receiver-clan-id", out retrievedReceiverClan),
                Times.Once);
            objectManager.Verify(
                manager => manager.TryGetObjectWithLogging("sender-clan-id", out retrievedSenderClan),
                Times.Once);
            objectManager.Verify(
                manager => manager.TryGetObjectWithLogging("sender-hero-id", out retrievedSenderHero),
                Times.Once);
            objectManager.VerifyNoOtherCalls();
        });
    }

    [Fact]
    public void NetworkGiftSettlementOwnership_SettlementIsNotGiftable_IsIgnored()
    {
        var peer = ObjectHelper.SkipConstructor<NetPeer>();
        var player = ObjectHelper.SkipConstructor<Player>();
        SetField(player, "ClanId", "sender-clan-id");
        SetField(player, "HeroId", "sender-hero-id");
        var settlement = ObjectHelper.SkipConstructor<Settlement>();
        settlement.Town = ObjectHelper.SkipConstructor<Town>();
        settlement.Town.IsOwnerUnassigned = true;
        var senderClan = ObjectHelper.SkipConstructor<Clan>();
        var senderHero = ObjectHelper.SkipConstructor<Hero>();
        var receiverClan = ObjectHelper.SkipConstructor<Clan>();
        var kingdom = ObjectHelper.SkipConstructor<Kingdom>();

        senderClan._kingdom = kingdom;
        senderClan._leader = senderHero;
        receiverClan._kingdom = kingdom;
        kingdom._rulingClan = senderClan;

        var objectManager = new Mock<IObjectManager>();
        var playerManager = new Mock<IPlayerManager>();
        playerManager
            .Setup(manager => manager.TryGetPlayer(peer, out player))
            .Returns(true);
        SetupGiftObjects(objectManager, settlement, receiverClan, senderClan, senderHero);

        var handler = CreateNetworkGiftSettlementOwnershipHandler(objectManager.Object, playerManager.Object);

        RunWithBoundGameThread(() =>
        {
            handler(new MessagePayload<NetworkGiftSettlementOwnership>(
            peer,
            new NetworkGiftSettlementOwnership("settlement-id", "receiver-clan-id")));
            DrainGameThread();

            Clan retrievedReceiverClan = null!;
            Clan retrievedSenderClan = null!;
            Settlement retrievedSettlement = null!;
            Hero retrievedSenderHero = null!;
            objectManager.Verify(
                manager => manager.TryGetObjectWithLogging("settlement-id", out retrievedSettlement),
                Times.Once);
            objectManager.Verify(
                manager => manager.TryGetObjectWithLogging("receiver-clan-id", out retrievedReceiverClan),
                Times.Once);
            objectManager.Verify(
                manager => manager.TryGetObjectWithLogging("sender-clan-id", out retrievedSenderClan),
                Times.Once);
            objectManager.Verify(
                manager => manager.TryGetObjectWithLogging("sender-hero-id", out retrievedSenderHero),
                Times.Once);
            objectManager.VerifyNoOtherCalls();
        });
    }

    [Fact]
    public void NetworkGiftSettlementOwnership_SettlementIsNotOwnedBySender_IsIgnored()
    {
        var peer = ObjectHelper.SkipConstructor<NetPeer>();
        var player = ObjectHelper.SkipConstructor<Player>();
        SetField(player, "ClanId", "sender-clan-id");
        SetField(player, "HeroId", "sender-hero-id");
        var settlement = ObjectHelper.SkipConstructor<Settlement>();
        var town = ObjectHelper.SkipConstructor<Town>();
        settlement.Town = town;

        var senderClan = ObjectHelper.SkipConstructor<Clan>();
        var senderHero = ObjectHelper.SkipConstructor<Hero>();
        var actualOwnerClan = ObjectHelper.SkipConstructor<Clan>();
        var receiverClan = ObjectHelper.SkipConstructor<Clan>();
        var kingdom = ObjectHelper.SkipConstructor<Kingdom>();

        senderClan._kingdom = kingdom;
        senderClan._leader = senderHero;
        receiverClan._kingdom = kingdom;
        kingdom._rulingClan = senderClan;
        SetField(town, "_ownerClan", actualOwnerClan);

        var objectManager = new Mock<IObjectManager>();
        var playerManager = new Mock<IPlayerManager>();
        playerManager
            .Setup(manager => manager.TryGetPlayer(peer, out player))
            .Returns(true);

        SetupGiftObjects(objectManager, settlement, receiverClan, senderClan, senderHero);

        var handler = CreateNetworkGiftSettlementOwnershipHandler(objectManager.Object, playerManager.Object);

        RunWithBoundGameThread(() =>
        {
            handler(new MessagePayload<NetworkGiftSettlementOwnership>(
            peer,
            new NetworkGiftSettlementOwnership("settlement-id", "receiver-clan-id")));
            DrainGameThread();

            Clan retrievedReceiverClan = null!;
            Clan retrievedSenderClan = null!;
            Settlement retrievedSettlement = null!;
            Hero retrievedSenderHero = null!;
            objectManager.Verify(
                manager => manager.TryGetObjectWithLogging("settlement-id", out retrievedSettlement),
                Times.Once);
            objectManager.Verify(
                manager => manager.TryGetObjectWithLogging("receiver-clan-id", out retrievedReceiverClan),
                Times.Once);
            objectManager.Verify(
                manager => manager.TryGetObjectWithLogging("sender-clan-id", out retrievedSenderClan),
                Times.Once);
            objectManager.Verify(
                manager => manager.TryGetObjectWithLogging("sender-hero-id", out retrievedSenderHero),
                Times.Once);
            objectManager.VerifyNoOtherCalls();
        });
    }

    [Fact]
    public void NetworkGiftSettlementOwnership_ShouldGift()
    {
        GameBootStrap.Initialize();
        var peer = ObjectHelper.SkipConstructor<NetPeer>();
        var player = ObjectHelper.SkipConstructor<Player>();
        SetField(player, "ClanId", "sender-clan-id");
        SetField(player, "HeroId", "sender-hero-id");
        var settlement = ObjectHelper.SkipConstructor<Settlement>();
        var town = ObjectHelper.SkipConstructor<Town>();
        var townOwner = ObjectHelper.SkipConstructor<PartyBase>();
        townOwner.Settlement = settlement;
        settlement.Town = town;
        settlement.Party = townOwner;
        town._owner = townOwner;
        town._governor = null;
        settlement._boundVillages = new MBList<Village> { };

        var senderClan = ObjectHelper.SkipConstructor<Clan>();
        var senderHero = ObjectHelper.SkipConstructor<Hero>();
        var actualOwnerClan = ObjectHelper.SkipConstructor<Clan>();
        var receiverClan = ObjectHelper.SkipConstructor<Clan>();
        var receiverHero = ObjectHelper.SkipConstructor<Hero>();
        var kingdom = ObjectHelper.SkipConstructor<Kingdom>();
        senderClan._kingdom = kingdom;
        senderClan._leader = senderHero;
        senderClan._fiefsCache = new MBList<Town>
        {
            settlement.Town
        };
        senderClan._settlementsCache = new MBList<Settlement>
        {
            settlement
        };
        kingdom._fiefsCache = new MBList<Town>
        {
            settlement.Town
        };
        kingdom._settlementsCache = new MBList<Settlement>
        {
            settlement
        };
        kingdom._townsCache = new MBList<Town>
        {
            settlement.Town
        };
        senderHero._clan = senderClan;
        receiverClan._kingdom = kingdom;
        receiverClan._leader = receiverHero;
        receiverClan._fiefsCache = new MBList<Town>();
        receiverClan._settlementsCache = new MBList<Settlement>();
        receiverHero._clan = receiverClan;
        kingdom._rulingClan = senderClan;
        SetField(town, "_ownerClan", senderClan);

        var mainCharacter = ObjectHelper.SkipConstructor<CharacterObject>();
        mainCharacter._heroObject = senderHero;
        senderHero._characterObject = mainCharacter;

        Game.Current.PlayerTroop = mainCharacter;

        var objectManager = new Mock<IObjectManager>();
        var playerManager = new Mock<IPlayerManager>();
        playerManager
            .Setup(manager => manager.TryGetPlayer(peer, out player))
            .Returns(true);
        SetupGiftObjects(objectManager, settlement, receiverClan, senderClan, senderHero);

        var handler = CreateNetworkGiftSettlementOwnershipHandler(objectManager.Object, playerManager.Object);

        RunWithBoundGameThread(() =>
        {
            var initialRelation = receiverClan.GetRelationWithClan(senderClan);
            handler(new MessagePayload<NetworkGiftSettlementOwnership>(
            peer,
            new NetworkGiftSettlementOwnership("settlement-id", "receiver-clan-id")));
            DrainGameThread();
            var adjustedRelation = receiverClan.GetRelationWithClan(senderClan);
            Clan retrievedReceiverClan = null!;
            Clan retrievedSenderClan = null!;
            Settlement retrievedSettlement = null!;
            Hero retrievedSenderHero = null!;
            objectManager.Verify(
                manager => manager.TryGetObjectWithLogging("settlement-id", out retrievedSettlement),
                Times.Once);
            objectManager.Verify(
                manager => manager.TryGetObjectWithLogging("receiver-clan-id", out retrievedReceiverClan),
                Times.Once);
            objectManager.Verify(
                manager => manager.TryGetObjectWithLogging("sender-clan-id", out retrievedSenderClan),
                Times.Once);
            objectManager.Verify(
                manager => manager.TryGetObjectWithLogging("sender-hero-id", out retrievedSenderHero),
                Times.Once);
            objectManager.VerifyNoOtherCalls();
            Assert.Equal(0, initialRelation);
            Assert.Equal(20, adjustedRelation);
            Assert.Equal(receiverClan, settlement.OwnerClan);
        });
    }

    private static void RunWithBoundGameThread(Action action)
    {
        bool ownsGameThreadMark = GameThread.Instance.GameThreadId == 0;
        if (ownsGameThreadMark)
            GameThread.Instance.MarkGameThread();
        try
        {
            action();
        }
        finally
        {
            if (ownsGameThreadMark)
                GameThread.Instance.RestoreGameThread(0);
        }
    }

    private static KingdomHandler CreateHandler(IObjectManager objectManager)
    {
        return new KingdomHandler(
            new Mock<IMessageBroker>().Object,
            new Mock<INetwork>().Object,
            objectManager,
            new Mock<IPlayerManager>().Object,
            new Mock<IKingdomDecisionVoteManager>().Object,
            new Mock<IKingdomMembershipState>().Object,
            new Mock<IKingdomInterface>().Object,
            new Mock<IKingdomCreator>().Object);
    }

    private static Action<MessagePayload<RemoveDecision>> CreateRemoveDecisionHandler(
        IObjectManager objectManager,
        IKingdomDecisionVoteManager voteManager,
        IKingdomInterface kingdomInterface)
    {
        Action<MessagePayload<RemoveDecision>> removeDecisionHandler = null!;
        var messageBroker = new Mock<IMessageBroker>();
        messageBroker
            .Setup(broker => broker.Subscribe(It.IsAny<Action<MessagePayload<RemoveDecision>>>()!))
            .Callback<Action<MessagePayload<RemoveDecision>>>(handler => removeDecisionHandler = handler);
        _ = new KingdomHandler(
            messageBroker.Object,
            new Mock<INetwork>().Object,
            objectManager,
            new Mock<IPlayerManager>().Object,
            voteManager,
            new Mock<IKingdomMembershipState>().Object,
            kingdomInterface,
            new Mock<IKingdomCreator>().Object);
        return removeDecisionHandler;
    }

    private static bool TryGetCulture(KingdomHandler handler, string cultureId, out CultureObject culture)
    {
        MethodInfo methodInfo = typeof(KingdomHandler).GetMethod("TryGetCulture", BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new NullReferenceException("TryGetCulture method was not found.");
        object[] args = { cultureId, null! };
        bool result = (bool)(methodInfo.Invoke(handler, args) ?? false);
        culture = (CultureObject)args[1];
        return result;
    }
    private static void DrainGameThread()
    {
        GameThread.Run(() => { }, blocking: true);
    }

    private static void SetField(object target, string fieldName, object value)
    {
        var field = target.GetType().GetField(
            fieldName,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            ?? throw new MissingFieldException(target.GetType().FullName, fieldName);

        field.SetValue(target, value);
    }

    private static Action<MessagePayload<NetworkGiftSettlementOwnership>> CreateNetworkGiftSettlementOwnershipHandler(
        IObjectManager objectManager,
        IPlayerManager playerManager)
    {
        Action<MessagePayload<NetworkGiftSettlementOwnership>> handler = null!;
        var messageBroker = new Mock<IMessageBroker>();
        messageBroker
            .Setup(broker => broker.Subscribe(It.IsAny<Action<MessagePayload<NetworkGiftSettlementOwnership>>>()!))
            .Callback<Action<MessagePayload<NetworkGiftSettlementOwnership>>>(callback => handler = callback);

        _ = new KingdomHandler(
            messageBroker.Object,
            new Mock<INetwork>().Object,
            objectManager,
            playerManager,
            new Mock<IKingdomDecisionVoteManager>().Object,
            new Mock<IKingdomMembershipState>().Object,
            new Mock<IKingdomInterface>().Object,
            new Mock<IKingdomCreator>().Object);

        return handler;
    }

    private static void SetupGiftObjects(
        Mock<IObjectManager> objectManager,
        Settlement settlement,
        Clan receiverClan,
        Clan senderClan,
        Hero senderHero)
    {
        objectManager
            .Setup(manager => manager.TryGetObjectWithLogging("settlement-id", out settlement))
            .Returns(settlement != null);
        objectManager
            .Setup(manager => manager.TryGetObjectWithLogging("receiver-clan-id", out receiverClan))
            .Returns(receiverClan != null);
        objectManager
            .Setup(manager => manager.TryGetObjectWithLogging("sender-clan-id", out senderClan))
            .Returns(senderClan != null);
        objectManager
            .Setup(manager => manager.TryGetObjectWithLogging("sender-hero-id", out senderHero))
            .Returns(senderHero != null);
    }
}
