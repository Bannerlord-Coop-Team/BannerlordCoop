using Common;
using Common.Messaging;
using Common.Network;
using Common.Network.Messages;
using Common.Tests.Utils;
using Common.Util;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.MobileParties.Messages.Behavior;
using GameInterface.Services.Players;
using GameInterface.Services.Players.Data;
using GameInterface.Services.Settlements;
using GameInterface.Services.Settlements.Handlers;
using GameInterface.Services.Settlements.Messages;
using LiteNetLib;
using Moq;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;
using Xunit;

namespace GameInterface.Tests.Services.Settlements;

[Collection(ModInformationRoleCollection.Name)]
public class SettlementMenuAccessHandlerTests : IDisposable
{
    private readonly bool wasServer = ModInformation.IsServer;
    private readonly TestMessageBroker broker = new();
    private readonly Mock<INetwork> network = new();
    private readonly Mock<IObjectManager> objects = new();
    private readonly Mock<IPlayerManager> players = new();
    private readonly NetPeer firstPeer = ObjectHelper.SkipConstructor<NetPeer>();
    private readonly NetPeer secondPeer = ObjectHelper.SkipConstructor<NetPeer>();
    private readonly Hero firstHero = ObjectHelper.SkipConstructor<Hero>();
    private readonly Hero secondHero = ObjectHelper.SkipConstructor<Hero>();
    private readonly Settlement settlement = ObjectHelper.SkipConstructor<Settlement>();
    private readonly List<NetworkSettlementMenuAccess> replies = new();
    private readonly SettlementMenuAccess access;
    private readonly SettlementMenuAccessHandler handler;
    private bool firstDisconnected;

    static SettlementMenuAccessHandlerTests()
        => RuntimeHelpers.RunModuleConstructor(typeof(Coop.Tests.Mocks.TestNetwork).Module.ModuleHandle);

    public SettlementMenuAccessHandlerTests()
    {
        ModInformation.IsServer = true;
        var clan = ObjectHelper.SkipConstructor<Clan>();
        settlement.Town = ObjectHelper.SkipConstructor<Town>();
        settlement.Town._ownerClan = clan;
        firstHero._clan = secondHero._clan = clan;
        firstHero._stayingInSettlement = secondHero._stayingInSettlement = settlement;
        var firstPlayer = new Player("first", "first-hero", "first-party", "clan", "first-character");
        var secondPlayer = new Player("second", "second-hero", "second-party", "clan", "second-character");
        players.Setup(p => p.TryGetPlayer(It.Is<NetPeer>(peer => ReferenceEquals(peer, firstPeer)), out firstPlayer)).Returns(true);
        players.Setup(p => p.TryGetPlayer(It.Is<NetPeer>(peer => ReferenceEquals(peer, secondPeer)), out secondPlayer)).Returns(true);
        players.SetupGet(p => p.Players).Returns(new[] { firstPlayer, secondPlayer });
        players.Setup(p => p.IsConnected(firstPlayer)).Returns(() => !firstDisconnected);
        players.Setup(p => p.IsConnected(secondPlayer)).Returns(true);
        SetupObject("first-hero", firstHero);
        SetupObject("second-hero", secondHero);
        SetupObject("town_ES1", settlement);
        network.Setup(n => n.Send(It.IsAny<NetPeer>(), It.IsAny<IMessage>()))
            .Callback<NetPeer, IMessage>((peer, message) => replies.Add((NetworkSettlementMenuAccess)message));
        access = new SettlementMenuAccess(network.Object, objects.Object);
        handler = new SettlementMenuAccessHandler(broker, network.Object, players.Object, objects.Object, access);
    }

    [Fact]
    public void CompetingClientRequestsGrantOnlyFirstRequest()
    {
        broker.Publish(firstPeer, new RequestSettlementMenuAccess("town_ES1", "manage_garrison", true));
        broker.Publish(secondPeer, new RequestSettlementMenuAccess("town_ES1", "manage_garrison", true));
        Drain();

        Assert.Collection(replies, reply => Assert.True(reply.Granted), reply => Assert.False(reply.Granted));
        Assert.Equal("first-hero", Assert.Single(access.GetOpenMenus()).HeroId);
    }

    [Fact]
    public void DisconnectReleasesOccupiedMenuAndBroadcastsAvailability()
    {
        broker.Publish(firstPeer, new RequestSettlementMenuAccess("town_ES1", "open_stash", true));
        Drain();
        firstDisconnected = true;
        broker.Publish(this, new PlayerConnectionStateChanged());
        Drain();

        Assert.Empty(access.GetOpenMenus());
        network.Verify(n => n.SendAll(It.Is<NetworkSettlementMenusChanged>(m => m.Menus.Length == 0)), Times.Once);
    }

    [Fact]
    public void RequestAfterLeavingSettlementIsRejected()
    {
        firstHero._stayingInSettlement = null;
        broker.Publish(firstPeer, new RequestSettlementMenuAccess("town_ES1", "open_stash", true));
        Drain();

        Assert.False(Assert.Single(replies).Granted);
        Assert.Empty(access.GetOpenMenus());
    }

    [Fact]
    public void ClosingMenuAllowsWaitingPlayerToOpenIt()
    {
        broker.Publish(firstPeer, new RequestSettlementMenuAccess("town_ES1", "open_stash", true));
        broker.Publish(firstPeer, new RequestSettlementMenuAccess("town_ES1", "open_stash", false));
        broker.Publish(secondPeer, new RequestSettlementMenuAccess("town_ES1", "open_stash", true));
        Drain();

        Assert.All(replies, reply => Assert.True(reply.Granted));
        Assert.Equal("second-hero", Assert.Single(access.GetOpenMenus()).HeroId);
    }

    [Fact]
    public void LeavingSettlementReleasesMenuForAnotherPlayer()
    {
        broker.Publish(firstPeer, new RequestSettlementMenuAccess("town_ES1", "open_stash", true));
        Drain();
        firstHero._stayingInSettlement = null;
        broker.Publish(this, new PartyOccupancyChanged(null));
        broker.Publish(secondPeer, new RequestSettlementMenuAccess("town_ES1", "open_stash", true));
        Drain();

        Assert.All(replies, reply => Assert.True(reply.Granted));
        Assert.Equal("second-hero", Assert.Single(access.GetOpenMenus()).HeroId);
    }

    [Fact]
    public void ConcurrentRequestsForDifferentMenusBothSucceed()
    {
        broker.Publish(firstPeer, new RequestSettlementMenuAccess("town_ES1", "manage_garrison", true));
        broker.Publish(secondPeer, new RequestSettlementMenuAccess("town_ES1", "open_stash", true));
        Drain();

        Assert.Equal(2, replies.Count);
        Assert.All(replies, reply => Assert.True(reply.Granted));
        Assert.Equal(2, access.GetOpenMenus().Length);
    }

    [Fact]
    public void RequestAfterLeavingClanIsRejected()
    {
        firstHero._clan = ObjectHelper.SkipConstructor<Clan>();
        broker.Publish(firstPeer, new RequestSettlementMenuAccess("town_ES1", "open_stash", true));
        Drain();

        Assert.False(Assert.Single(replies).Granted);
        Assert.Empty(access.GetOpenMenus());
    }

    [Fact]
    public void EmptySnapshotClearsClientMenuLocksAfterSerialization()
    {
        access.TryAcquire("town_ES1", "open_stash", "first-hero");
        var snapshot = ProtoBuf.Serializer.DeepClone(new NetworkSettlementMenusChanged(Array.Empty<SettlementMenuUse>()));

        access.Update(snapshot.Menus);

        Assert.Empty(access.GetOpenMenus());
    }

    private void SetupObject<T>(string id, T value) where T : class
    {
        objects.Setup(o => o.TryGetObjectWithLogging(id, out value)).Returns(true);
        objects.Setup(o => o.TryGetObject(id, out value)).Returns(true);
    }

    private static void Drain() => GameThread.Run(() => { }, blocking: true);

    public void Dispose()
    {
        Drain();
        handler.Dispose();
        ModInformation.IsServer = wasServer;
    }
}
