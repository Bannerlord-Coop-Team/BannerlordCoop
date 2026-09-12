using Common;
using Common.Messaging;
using Common.Network;
using Common.Util;
using GameInterface.Services.Clans.Handlers;
using GameInterface.Services.Clans.Messages;
using GameInterface.Services.Clans.Patches;
using GameInterface.Services.ObjectManager;
using Moq;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.Serialization;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Library;
using Xunit;
using NetworkMessageHandler = System.Action<Common.Messaging.MessagePayload<GameInterface.Services.Clans.Messages.NetworkInitializeSettlementRebelClan>>;
using ServerMessageHandler = System.Action<Common.Messaging.MessagePayload<GameInterface.Services.Clans.Messages.SettlementRebelClanInitialized>>;

namespace GameInterface.Tests.Services.Clans;

[Collection(ModInformationRoleCollection.Name)]
public class SettlementRebelClanInitializationHandlerTests
{
    static SettlementRebelClanInitializationHandlerTests()
    {
        RuntimeHelpers.RunModuleConstructor(typeof(Coop.Tests.Mocks.TestNetwork).Module.ModuleHandle);
    }

    [Fact]
    public void CreateSettlementRebelClanPostfix_Server_PublishesFactorySnapshot()
    {
        var clan = ObjectHelper.SkipConstructor<Clan>();
        var messages = new List<SettlementRebelClanInitialized>();
        Action<MessagePayload<SettlementRebelClanInitialized>> capture = payload => messages.Add(payload.What);
        bool wasServer = ModInformation.IsServer;

        MessageBroker.Instance.Subscribe(capture);
        ModInformation.IsServer = true;
        try
        {
            ClanPatches.CreateSettlementRebelClanPostfix(clan);
        }
        finally
        {
            ModInformation.IsServer = wasServer;
            MessageBroker.Instance.Unsubscribe(capture);
        }

        SettlementRebelClanInitialized message = Assert.Single(messages);
        Assert.Same(clan, message.Clan);
    }

    [Fact]
    public void CompletedRebelClan_Server_SendsFactoryFieldSnapshot()
    {
        var clan = ObjectHelper.SkipConstructor<Clan>();
        var culture = ObjectHelper.SkipConstructor<CultureObject>();
        var leader = ObjectHelper.SkipConstructor<Hero>();
        var settlement = ObjectHelper.SkipConstructor<Settlement>();
        var banner = CreateBanner();

        clan.Culture = culture;
        clan._leader = leader;
        clan._banner = banner;
        clan._tier = 3;
        clan.InitialHomeSettlement = settlement;
        clan._home = settlement;
        clan.Color = 11;
        clan.Color2 = 12;
        clan.BannerBackgroundColorPrimary = 13;
        clan.BannerBackgroundColorSecondary = 14;
        clan.BannerIconColor = 15;
        clan.IsRebelClan = true;
        clan.IsNoble = true;

        var messageBroker = new Mock<IMessageBroker>();
        ServerMessageHandler serverHandler = null!;
        messageBroker
            .Setup(b => b.Subscribe(It.IsAny<ServerMessageHandler>()))
            .Callback<ServerMessageHandler>(handler => serverHandler = handler);

        var objectManager = new Mock<IObjectManager>();
        SetupId(objectManager, clan, "clan-1");
        SetupId(objectManager, culture, "culture-1");
        SetupId(objectManager, leader, "hero-1");
        SetupId(objectManager, settlement, "settlement-1");

        IMessage sentMessage = null!;
        var network = new Mock<INetwork>();
        network.Setup(n => n.SendAll(It.IsAny<IMessage>()))
            .Callback<IMessage>(message => sentMessage = message);

        using var handler = new SettlementRebelClanInitializationHandler(
            messageBroker.Object,
            objectManager.Object,
            network.Object);

        Assert.NotNull(serverHandler);
        serverHandler(new MessagePayload<SettlementRebelClanInitialized>(
            this,
            new SettlementRebelClanInitialized(clan)));

        var message = Assert.IsType<NetworkInitializeSettlementRebelClan>(sentMessage);
        Assert.Equal("clan-1", message.ClanId);
        Assert.Equal("culture-1", message.CultureId);
        Assert.Equal("hero-1", message.LeaderId);
        Assert.Equal("settlement-1", message.InitialHomeSettlementId);
        Assert.Equal("settlement-1", message.HomeSettlementId);
        Assert.Equal(banner.Serialize(), message.BannerCode);
        Assert.Equal(3, message.Tier);
        Assert.Equal(11u, message.Color);
        Assert.Equal(12u, message.Color2);
        Assert.Equal(13u, message.BannerBackgroundColorPrimary);
        Assert.Equal(14u, message.BannerBackgroundColorSecondary);
        Assert.Equal(15u, message.BannerIconColor);
        Assert.True(message.IsRebelClan);
        Assert.True(message.IsNoble);
    }

    [Fact]
    public void RebelClanSnapshot_Client_AppliesCompleteFactoryState()
    {
        var clan = (Clan)FormatterServices.GetUninitializedObject(typeof(Clan));
        var culture = (CultureObject)FormatterServices.GetUninitializedObject(typeof(CultureObject));
        var leader = (Hero)FormatterServices.GetUninitializedObject(typeof(Hero));
        var settlement = (Settlement)FormatterServices.GetUninitializedObject(typeof(Settlement));
        string bannerCode = CreateBanner().Serialize();

        var messageBroker = new Mock<IMessageBroker>();
        NetworkMessageHandler networkHandler = null!;
        messageBroker
            .Setup(b => b.Subscribe(It.IsAny<NetworkMessageHandler>()))
            .Callback<NetworkMessageHandler>(handler => networkHandler = handler);

        var objectManager = new Mock<IObjectManager>();
        SetupObject(objectManager, "clan-1", clan);
        SetupObject(objectManager, "culture-1", culture);
        SetupObject(objectManager, "hero-1", leader);
        SetupObject(objectManager, "settlement-1", settlement);

        using var handler = new SettlementRebelClanInitializationHandler(
            messageBroker.Object,
            objectManager.Object,
            new Mock<INetwork>().Object);

        var message = new NetworkInitializeSettlementRebelClan(
            "clan-1",
            "culture-1",
            "hero-1",
            "settlement-1",
            "settlement-1",
            bannerCode,
            3,
            11,
            12,
            13,
            14,
            15,
            true,
            true);

        Assert.NotNull(networkHandler);
        networkHandler(new MessagePayload<NetworkInitializeSettlementRebelClan>(this, message));
        GameThread.Run(() => { }, blocking: true);

        Assert.Same(culture, clan.Culture);
        Assert.Same(leader, clan.Leader);
        Assert.Equal(bannerCode, clan.Banner.Serialize());
        Assert.Equal(3, clan.Tier);
        Assert.Same(settlement, clan.InitialHomeSettlement);
        Assert.Same(settlement, clan.HomeSettlement);
        Assert.Equal(11u, clan.Color);
        Assert.Equal(12u, clan.Color2);
        Assert.Equal(13u, clan.BannerBackgroundColorPrimary);
        Assert.Equal(14u, clan.BannerBackgroundColorSecondary);
        Assert.Equal(15u, clan.BannerIconColor);
        Assert.True(clan.IsRebelClan);
        Assert.True(clan.IsNoble);
        Assert.True(clan._distanceToClosestNonAllyFortificationCacheDirty);
    }

    private static Banner CreateBanner()
    {
        var banner = new Banner();
        banner.BannerDataList.Add(new BannerData(1, 2, 3, Vec2.One, Vec2.Zero, true, true, 0f));
        return banner;
    }

    private static void SetupId<T>(Mock<IObjectManager> objectManager, T instance, string id)
        where T : class
    {
        objectManager.Setup(manager => manager.TryGetIdWithLogging(instance, out id)).Returns(true);
    }

    private static void SetupObject<T>(Mock<IObjectManager> objectManager, string id, T instance)
        where T : class
    {
        objectManager.Setup(manager => manager.TryGetObjectWithLogging(id, out instance)).Returns(true);
    }
}
