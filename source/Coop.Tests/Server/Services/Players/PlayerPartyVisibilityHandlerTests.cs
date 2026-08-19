using Common;
using Common.Network;
using Common.Network.Messages;
using Common.Tests.Utils;
using Coop.Core.Server.Connections;
using Coop.Core.Server.Connections.Messages;
using Coop.Core.Server.Services.Players.Handlers;
using Coop.Core.Server.Services.Save.Messages;
using GameInterface.Services.MapEvents.Messages;
using GameInterface.Services.MapEvents.Messages.Leave;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.PlayerCaptivityService.Messages;
using GameInterface.Services.Players;
using GameInterface.Services.Players.Data;
using GameInterface.Services.SiegeEvents.Interfaces;
using HarmonyLib;
using LiteNetLib;
using Moq;
using System;
using System.Runtime.Serialization;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.Siege;
using Xunit;

namespace Coop.Tests.Server.Services.Players;

[Collection(ModInformationRoleCollection.Name)]
public class PlayerPartyVisibilityHandlerTests : IDisposable
{
    private readonly bool wasServer = ModInformation.IsServer;
    private readonly Campaign? previousCampaign;
    private readonly Type sandBoxViewSubModuleType;
    private readonly object? previousSandBoxViewSubModule;

    public PlayerPartyVisibilityHandlerTests()
    {
        ModInformation.IsServer = true;
        previousCampaign = Campaign.Current;

        var campaign = (Campaign)FormatterServices.GetUninitializedObject(typeof(Campaign));
        campaign.CampaignEventDispatcher = new CampaignEventDispatcher(Array.Empty<CampaignEventReceiver>());
        Campaign.Current = campaign;

        sandBoxViewSubModuleType = Type.GetType(
            "SandBox.View.SandBoxViewSubModule, SandBox.View",
            throwOnError: true)!;
        var visualManagerType = Type.GetType(
            "SandBox.View.SandBoxViewVisualManager, SandBox.View",
            throwOnError: true)!;
        var instanceField = AccessTools.Field(sandBoxViewSubModuleType, "_instance");
        previousSandBoxViewSubModule = instanceField.GetValue(null);

        var subModule = FormatterServices.GetUninitializedObject(sandBoxViewSubModuleType);
        AccessTools.Field(sandBoxViewSubModuleType, "_sandBoxViewVisualManager")
            .SetValue(subModule, Activator.CreateInstance(visualManagerType));
        instanceField.SetValue(null, subModule);
    }

    public void Dispose()
    {
        ModInformation.IsServer = wasServer;
        Campaign.Current = previousCampaign;
        AccessTools.Field(sandBoxViewSubModuleType, "_instance")
            .SetValue(null, previousSandBoxViewSubModule);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void SavedPlayerRegistrationsRestored_PersistsOfflinePartyAsInactiveAndHidden(bool isActive)
    {
        var player = CreatePlayer();
        var party = CreateParty(isActive);
        var playerManager = new Mock<IPlayerManager>();
        playerManager.SetupGet(manager => manager.Players).Returns(new[] { player });
        playerManager.Setup(manager => manager.IsConnected(player)).Returns(false);

        var objectManager = new Mock<IObjectManager>();
        objectManager
            .Setup(manager => manager.TryGetObjectWithLogging(player.MobilePartyId, out party))
            .Returns(true);

        var broker = new TestMessageBroker();
        using var handler = new PlayerPartyVisibilityHandler(
            broker,
            playerManager.Object,
            Mock.Of<IConnectionCollection>(),
            objectManager.Object,
            Mock.Of<INetwork>(),
            Mock.Of<ISiegeEventInterface>());

        broker.Publish(this, new SavedPlayerRegistrationsRestored());

        Assert.False(party.IsActive);
        Assert.False(party.IsVisible);
    }

    [Fact]
    public void SavedPlayerRegistrationsRestored_LeavesConnectedPartyActive()
    {
        var player = CreatePlayer();
        var party = CreateParty();
        var playerManager = new Mock<IPlayerManager>();
        playerManager.SetupGet(manager => manager.Players).Returns(new[] { player });
        playerManager.Setup(manager => manager.IsConnected(player)).Returns(true);

        var objectManager = new Mock<IObjectManager>();
        var broker = new TestMessageBroker();
        using var handler = new PlayerPartyVisibilityHandler(
            broker,
            playerManager.Object,
            Mock.Of<IConnectionCollection>(),
            objectManager.Object,
            Mock.Of<INetwork>(),
            Mock.Of<ISiegeEventInterface>());

        broker.Publish(this, new SavedPlayerRegistrationsRestored());

        Assert.True(party.IsActive);
        objectManager.VerifyNoOtherCalls();
    }

    [Fact]
    public void SavedPlayerRegistrationsRestored_LeavesSiegeBeforeParking()
    {
        var player = CreatePlayer();
        var party = CreateParty();
        party._besiegerCamp = (BesiegerCamp)FormatterServices.GetUninitializedObject(typeof(BesiegerCamp));

        var playerManager = new Mock<IPlayerManager>();
        playerManager.SetupGet(manager => manager.Players).Returns(new[] { player });
        playerManager.Setup(manager => manager.IsConnected(player)).Returns(false);

        var objectManager = new Mock<IObjectManager>();
        objectManager
            .Setup(manager => manager.TryGetObjectWithLogging(player.MobilePartyId, out party))
            .Returns(true);

        var siegeEventInterface = new Mock<ISiegeEventInterface>();
        var broker = new TestMessageBroker();
        using var handler = new PlayerPartyVisibilityHandler(
            broker,
            playerManager.Object,
            Mock.Of<IConnectionCollection>(),
            objectManager.Object,
            Mock.Of<INetwork>(),
            siegeEventInterface.Object);

        broker.Publish(this, new SavedPlayerRegistrationsRestored());

        siegeEventInterface.Verify(value => value.BreakSiegeForPartyOnly(party), Times.Once);
        Assert.False(party.IsActive);
    }

    [Fact]
    public void CampaignEntry_LeavesSettlementPartyParkedUntilSynchronizationCompletes()
    {
        var peer = (NetPeer)FormatterServices.GetUninitializedObject(typeof(NetPeer));
        var player = CreatePlayer();
        var party = CreateParty(isActive: false);
        var settlement = (Settlement)FormatterServices.GetUninitializedObject(typeof(Settlement));
        party._currentSettlement = settlement;
        var playerManager = new Mock<IPlayerManager>();
        playerManager
            .Setup(manager => manager.TryGetPlayer(peer, out player))
            .Returns(true);
        playerManager
            .Setup(manager => manager.TryGetPeer(player.ControllerId, out peer))
            .Returns(true);

        var objectManager = new Mock<IObjectManager>();
        objectManager
            .Setup(manager => manager.TryGetObjectWithLogging(player.MobilePartyId, out party))
            .Returns(true);

        var broker = new TestMessageBroker();
        using var handler = new PlayerPartyVisibilityHandler(
            broker,
            playerManager.Object,
            Mock.Of<IConnectionCollection>(),
            objectManager.Object,
            Mock.Of<INetwork>(),
            Mock.Of<ISiegeEventInterface>());

        broker.Publish(this, new PlayerCampaignEntered(peer));
        Assert.False(party.IsActive);

        broker.Publish(this, new PlayerCampaignSynchronized(peer));
        GameThread.Run(() => { }, blocking: true);

        Assert.True(party.IsActive);
        Assert.Same(settlement, party.CurrentSettlement);
    }

    [Fact]
    public void SynchronizationFromSupersededPeer_LeavesPartyParked()
    {
        var supersededPeer = (NetPeer)FormatterServices.GetUninitializedObject(typeof(NetPeer));
        var currentPeer = (NetPeer)FormatterServices.GetUninitializedObject(typeof(NetPeer));
        var player = CreatePlayer();
        var party = CreateParty(isActive: false);

        var playerManager = new Mock<IPlayerManager>();
        playerManager
            .Setup(manager => manager.TryGetPlayer(supersededPeer, out player))
            .Returns(true);
        playerManager
            .Setup(manager => manager.TryGetPeer(player.ControllerId, out currentPeer))
            .Returns(true);

        var objectManager = new Mock<IObjectManager>();
        var broker = new TestMessageBroker();
        using var handler = new PlayerPartyVisibilityHandler(
            broker,
            playerManager.Object,
            Mock.Of<IConnectionCollection>(),
            objectManager.Object,
            Mock.Of<INetwork>(),
            Mock.Of<ISiegeEventInterface>());

        broker.Publish(this, new PlayerCampaignSynchronized(supersededPeer));
        GameThread.Run(() => { }, blocking: true);

        Assert.False(party.IsActive);
        objectManager.VerifyNoOtherCalls();
    }

    [Fact]
    public void CaptivityRelease_OfflineOwner_LeavesPartyInactiveHiddenAndWithoutVisual()
    {
        var player = CreatePlayer();
        var party = CreateParty();
        var peer = (NetPeer)FormatterServices.GetUninitializedObject(typeof(NetPeer));
        var (playerManager, connectionCollection, objectManager) =
            CreateReleaseMocks(player, party, peer, isConnected: false, isSynchronized: false);
        var network = new Mock<INetwork>();
        var broker = new TestMessageBroker();
        using var handler = new PlayerPartyVisibilityHandler(
            broker,
            playerManager.Object,
            connectionCollection.Object,
            objectManager.Object,
            network.Object,
            Mock.Of<ISiegeEventInterface>());

        broker.Publish(this, new PlayerPartyReleasedFromCaptivity(party));

        Assert.False(party.IsActive);
        Assert.False(party.IsVisible);
        network.VerifyNoOtherCalls();
    }

    [Fact]
    public void CaptivityRelease_SynchronizedOwner_ActivatesParty()
    {
        var player = CreatePlayer();
        var party = CreateParty(isActive: false);
        party._currentSettlement = (Settlement)FormatterServices.GetUninitializedObject(typeof(Settlement));
        var peer = (NetPeer)FormatterServices.GetUninitializedObject(typeof(NetPeer));
        var (playerManager, connectionCollection, objectManager) =
            CreateReleaseMocks(player, party, peer, isConnected: true, isSynchronized: true);
        var broker = new TestMessageBroker();
        using var handler = new PlayerPartyVisibilityHandler(
            broker,
            playerManager.Object,
            connectionCollection.Object,
            objectManager.Object,
            Mock.Of<INetwork>(),
            Mock.Of<ISiegeEventInterface>());

        broker.Publish(this, new PlayerPartyReleasedFromCaptivity(party));

        Assert.True(party.IsActive);
    }

    [Fact]
    public void CaptivityRelease_LoadingOwner_LeavesPartyParked()
    {
        var player = CreatePlayer();
        var party = CreateParty();
        var peer = (NetPeer)FormatterServices.GetUninitializedObject(typeof(NetPeer));
        var (playerManager, connectionCollection, objectManager) =
            CreateReleaseMocks(player, party, peer, isConnected: true, isSynchronized: false);
        var broker = new TestMessageBroker();
        using var handler = new PlayerPartyVisibilityHandler(
            broker,
            playerManager.Object,
            connectionCollection.Object,
            objectManager.Object,
            Mock.Of<INetwork>(),
            Mock.Of<ISiegeEventInterface>());

        broker.Publish(this, new PlayerPartyReleasedFromCaptivity(party));

        Assert.False(party.IsActive);
        Assert.False(party.IsVisible);
    }

    [Fact]
    public void CaptivityRelease_LoadingOwner_ActivatesAfterCampaignSynchronization()
    {
        var player = CreatePlayer();
        var party = CreateParty();
        var peer = (NetPeer)FormatterServices.GetUninitializedObject(typeof(NetPeer));
        var (playerManager, connectionCollection, objectManager) =
            CreateReleaseMocks(player, party, peer, isConnected: true, isSynchronized: false);
        objectManager
            .Setup(manager => manager.TryGetObjectWithLogging(player.MobilePartyId, out party))
            .Returns(true);
        var broker = new TestMessageBroker();
        using var handler = new PlayerPartyVisibilityHandler(
            broker,
            playerManager.Object,
            connectionCollection.Object,
            objectManager.Object,
            Mock.Of<INetwork>(),
            Mock.Of<ISiegeEventInterface>());

        broker.Publish(this, new PlayerPartyReleasedFromCaptivity(party));
        Assert.False(party.IsActive);

        broker.Publish(this, new PlayerCampaignSynchronized(peer));
        GameThread.Run(() => { }, blocking: true);

        Assert.True(party.IsActive);
    }

    [Fact]
    public void CaptivityRelease_DeferredMapEventParking_RemainsInactiveAfterFinalization()
    {
        var player = CreatePlayer();
        var party = CreateParty();
        var peer = (NetPeer)FormatterServices.GetUninitializedObject(typeof(NetPeer));
        var mapEvent = (MapEvent)FormatterServices.GetUninitializedObject(typeof(MapEvent));
        var mapEventSide = (MapEventSide)FormatterServices.GetUninitializedObject(typeof(MapEventSide));
        AccessTools.Field(typeof(MapEventSide), "_mapEvent").SetValue(mapEventSide, mapEvent);
        party.Party._mapEventSide = mapEventSide;

        var (playerManager, connectionCollection, objectManager) =
            CreateReleaseMocks(player, party, peer, isConnected: false, isSynchronized: false);
        objectManager
            .Setup(manager => manager.TryGetObjectWithLogging(player.MobilePartyId, out party))
            .Returns(true);
        var broker = new TestMessageBroker();
        using var handler = new PlayerPartyVisibilityHandler(
            broker,
            playerManager.Object,
            connectionCollection.Object,
            objectManager.Object,
            Mock.Of<INetwork>(),
            Mock.Of<ISiegeEventInterface>());

        broker.Publish(this, new PlayerDisconnected(peer, default));
        GameThread.Run(() => { }, blocking: true);
        Assert.True(party.IsActive);
        Assert.Single(broker.GetMessagesFromType<PlayerDisconnectedFromMapEvent>());

        party.Party._mapEventSide = null;
        broker.Publish(this, new PlayerPartyReleasedFromCaptivity(party));
        Assert.False(party.IsActive);

        broker.Publish(this, new MapEventFinalized(mapEvent));

        Assert.False(party.IsActive);
        Assert.False(party.IsVisible);
    }

    [Fact]
    public void PlayerDisconnected_AfterClearingPeer_PublishesConnectionStateChanged()
    {
        var peer = (NetPeer)FormatterServices.GetUninitializedObject(typeof(NetPeer));
        var player = CreatePlayer();
        var party = CreateParty();
        var playerManager = new Mock<IPlayerManager>();
        playerManager
            .Setup(manager => manager.TryGetPlayer(peer, out player))
            .Returns(true);

        var objectManager = new Mock<IObjectManager>();
        objectManager
            .Setup(manager => manager.TryGetObjectWithLogging(player.MobilePartyId, out party))
            .Returns(true);

        var broker = new TestMessageBroker();
        using var handler = new PlayerPartyVisibilityHandler(
            broker,
            playerManager.Object,
            Mock.Of<IConnectionCollection>(),
            objectManager.Object,
            Mock.Of<INetwork>(),
            Mock.Of<ISiegeEventInterface>());

        broker.Publish(this, new PlayerDisconnected(peer, default));

        playerManager.Verify(manager => manager.ClearPeer(peer), Times.Once);
        Assert.Single(broker.GetMessagesFromType<PlayerConnectionStateChanged>());
    }

    private static (Mock<IPlayerManager>, Mock<IConnectionCollection>, Mock<IObjectManager>)
        CreateReleaseMocks(
            Player player,
            MobileParty party,
            NetPeer peer,
            bool isConnected,
            bool isSynchronized)
    {
        var playerManager = new Mock<IPlayerManager>();
        playerManager.SetupGet(manager => manager.Players).Returns(new[] { player });
        playerManager.Setup(manager => manager.TryGetPlayer(peer, out player)).Returns(true);
        playerManager.Setup(manager => manager.TryGetPeer(player.ControllerId, out peer)).Returns(true);
        playerManager.Setup(manager => manager.IsConnected(player)).Returns(isConnected);

        string partyId = player.MobilePartyId;
        var objectManager = new Mock<IObjectManager>();
        objectManager.Setup(manager => manager.TryGetIdWithLogging(party, out partyId)).Returns(true);

        var connectionCollection = new Mock<IConnectionCollection>();
        connectionCollection
            .Setup(collection => collection.HasCompletedCampaignSynchronization(peer))
            .Returns(isSynchronized);

        return (playerManager, connectionCollection, objectManager);
    }

    private static Player CreatePlayer() =>
        new Player("PlayerOne", "Hero_One", "Party_One", "Clan_One", "Character_One");

    private static MobileParty CreateParty(bool isActive = true)
    {
        var party = (MobileParty)FormatterServices.GetUninitializedObject(typeof(MobileParty));
        party.Party = (PartyBase)FormatterServices.GetUninitializedObject(typeof(PartyBase));
        party.Party.MobileParty = party;
        party.IsActive = isActive;
        party._isVisible = true;
        return party;
    }
}
