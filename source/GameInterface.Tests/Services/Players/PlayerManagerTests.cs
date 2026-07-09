using Common;
using Common.Util;
using Coop.Tests.Mocks;
using GameInterface.Services.Entity;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Players;
using GameInterface.Services.Players.Data;
using HarmonyLib;
using Moq;
using Serilog;
using System;
using System.Runtime.CompilerServices;
using System.Threading;
using TaleWorlds.CampaignSystem.Party;
using Xunit;

namespace GameInterface.Tests.Services.Players;

public class PlayerManagerTests
{
    private const string ControllerId = "PlayerOne";
    private const string PartyId = "PartyOne";

    static PlayerManagerTests()
    {
        RuntimeHelpers.RunModuleConstructor(typeof(Coop.Tests.Mocks.TestNetwork).Module.ModuleHandle);
    }
    private static PlayerManager CreatePlayerManager(out Mock<IObjectManager> objectManager)
    {
        objectManager = new Mock<IObjectManager>();
        var controllerIdProvider = new ControllerIdProvider();

        return new PlayerManager(new Mock<ILogger>().Object, objectManager.Object, controllerIdProvider);
    }
    [Fact]
    public void AddPlayer_MobileParty_InvalidatesBaseSpeedCache()
    {
        Assert.True(GameThread.Instance.IsInitialized, "game-loop pump was not initialized");

        var party = ObjectHelper.SkipConstructor<MobileParty>();
        party._partyPureSpeedLastCheckVersion = 42;

        var playerManager = CreatePlayerManager(out var objectManager);
        MobileParty resolvedParty = party;
        objectManager.Setup(o => o.TryGetObjectWithLogging<MobileParty>(PartyId, out resolvedParty))
            .Returns(true);

        var playerObjects = GetPlayerObjects();
        try
        {
            Assert.True(playerManager.AddPlayer(new Player(
                ControllerId,
                string.Empty,
                PartyId,
                string.Empty,
                string.Empty)));
            Assert.True(PlayerManager.TryGetControlledObjectInfo(party, out _));
            Assert.True(
                SpinWait.SpinUntil(() => party._partyPureSpeedLastCheckVersion == -1, TimeSpan.FromSeconds(5)),
                "player-party speed cache was not invalidated");
        }
        finally
        {
            playerObjects.Remove(party);
        }
    }
    [Fact]
    public void SetPeer_KnownController_AssociatesPeerWithPlayer()
    {
        var playerManager = CreatePlayerManager(out _);
        var player = new Player(ControllerId, string.Empty, string.Empty, string.Empty, string.Empty);
        var network = new TestNetwork();
        var peer = network.CreatePeer();

        Assert.True(playerManager.AddPlayer(player));

        playerManager.SetPeer(ControllerId, peer);

        Assert.True(playerManager.TryGetPlayer(peer, out var resolvedPlayer));
        Assert.Same(player, resolvedPlayer);
    }

    [Fact]
    public void SetPeer_UnknownController_DoesNotAssociatePeer()
    {
        var playerManager = CreatePlayerManager(out _);
        var network = new TestNetwork();
        var peer = network.CreatePeer();

        // No AddPlayer call, "UnknownController" was never registered.
        playerManager.SetPeer("UnknownController", peer);

        Assert.False(playerManager.TryGetPlayer(peer, out _));
    }

    [Fact]
    public void ClearPeer_AssociatedPeer_RemovesAssociation()
    {
        var playerManager = CreatePlayerManager(out _);
        var player = new Player(ControllerId, string.Empty, string.Empty, string.Empty, string.Empty);
        var network = new TestNetwork();
        var peer = network.CreatePeer();

        Assert.True(playerManager.AddPlayer(player));
        playerManager.SetPeer(ControllerId, peer);

        playerManager.ClearPeer(peer);

        Assert.False(playerManager.TryGetPlayer(peer, out _));
    }

    [Fact]
    public void TryGetPlayer_DifferentPeersSameController_ReturnsMostRecentlyAssociatedPeer()
    {
        var playerManager = CreatePlayerManager(out _);
        var player = new Player(ControllerId, string.Empty, string.Empty, string.Empty, string.Empty);
        var network = new TestNetwork();
        var firstPeer = network.CreatePeer();
        var secondPeer = network.CreatePeer();

        Assert.True(playerManager.AddPlayer(player));

        // Simulates a reconnect: same controllerId, new NetPeer.
        playerManager.SetPeer(ControllerId, firstPeer);
        playerManager.SetPeer(ControllerId, secondPeer);

        Assert.True(playerManager.TryGetPlayer(secondPeer, out var resolvedPlayer));
        Assert.Same(player, resolvedPlayer);

        // The stale first peer is untouched by re-associating the controller under a new peer,
        // the reconnect handler is responsible for calling ClearPeer(firstPeer) itself on disconnect.
        Assert.True(playerManager.TryGetPlayer(firstPeer, out _));
    }

    private static ConditionalWeakTable<object, ControlledObjectInfo> GetPlayerObjects() =>
        (ConditionalWeakTable<object, ControlledObjectInfo>)AccessTools
            .Field(typeof(PlayerManager), "PlayerObjects")
            .GetValue(null)!;
}