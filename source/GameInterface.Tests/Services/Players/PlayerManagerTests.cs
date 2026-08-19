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
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Party.PartyComponents;
using Xunit;

namespace GameInterface.Tests.Services.Players;

public class PlayerManagerTests
{
    private const string ControllerId = "PlayerOne";
    private const string HeroId = "HeroOne";
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
    public void AddPlayer_MobileParty_InvalidatesCaravansCaches()
    {
        Assert.True(GameThread.Instance.IsInitialized, "game-loop pump was not initialized");

        var hero = ObjectHelper.SkipConstructor<Hero>();
        hero.OwnedCaravans = new();

        // Populate hero.OwnedCaravans to invalidate caches of
        for (int i = 0; i < 5; i++)
        {
            MobileParty caravan = ObjectHelper.SkipConstructor<MobileParty>();
            caravan.Party = ObjectHelper.SkipConstructor<PartyBase>();
            caravan.Party.MobileParty = caravan;

            caravan._partyComponent = new CaravanPartyComponent(null, hero, null, false, null);
            caravan._partyComponent?.Initialize(caravan);

            caravan.Party._partyMemberSizeLastCheckVersion = 42;
            caravan.Party.MobileParty._partyPureSpeedLastCheckVersion = 84;

            hero.OwnedCaravans.Add(caravan.CaravanPartyComponent);
        }

        var playerManager = CreatePlayerManager(out var objectManager);
        Hero resolvedHero = hero;
        objectManager.Setup(o => o.TryGetObjectWithLogging<Hero>(HeroId, out hero))
            .Returns(true);

        var playerObjects = GetPlayerObjects();
        try
        {
            Assert.True(playerManager.AddPlayer(new Player(
                ControllerId,
                HeroId,
                string.Empty,
                string.Empty,
                string.Empty)));
            Assert.True(PlayerManager.TryGetControlledObjectInfo(hero, out _));
            Assert.NotNull(hero.OwnedCaravans);
            foreach (var ownedCaravan in hero.OwnedCaravans)
            {
                Assert.True(
                SpinWait.SpinUntil(() => ownedCaravan.Party._partyMemberSizeLastCheckVersion == -1, TimeSpan.FromSeconds(5)),
                "player-caravan member size limit cache was not invalidated");
                Assert.True(
                SpinWait.SpinUntil(() => ownedCaravan.Party.MobileParty._partyPureSpeedLastCheckVersion == -1, TimeSpan.FromSeconds(5)),
                "player-caravan speed cache was not invalidated");
            }
        }
        finally
        {
            playerObjects.Remove(hero);
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
        Assert.True(playerManager.TryGetPeer(ControllerId, out var resolvedPeer));
        Assert.Same(peer, resolvedPeer);
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
        Assert.False(playerManager.TryGetPeer(ControllerId, out _));
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
        Assert.True(playerManager.TryGetPeer(ControllerId, out var resolvedPeer));
        Assert.Same(secondPeer, resolvedPeer);

        // The stale first peer is untouched by re-associating the controller under a new peer,
        // the reconnect handler is responsible for calling ClearPeer(firstPeer) itself on disconnect.
        Assert.True(playerManager.TryGetPlayer(firstPeer, out _));

        playerManager.ClearPeer(firstPeer);

        Assert.True(playerManager.TryGetPeer(ControllerId, out resolvedPeer));
        Assert.Same(secondPeer, resolvedPeer);
    }

    [Fact]
    public void AddPlayer_SecondRegistrationForSameController_IsRefused()
    {
        var playerManager = CreatePlayerManager(out _);
        var registered = new Player(ControllerId, "FirstHero", string.Empty, string.Empty, string.Empty);
        var replacement = new Player(ControllerId, "SecondHero", string.Empty, string.Empty, string.Empty);

        Assert.True(playerManager.AddPlayer(registered));

        // A registration whose hero was gone used to send its owner through character creation,
        // and the character created there registered this same controller a second time. Player
        // has no value equality, so a set of instances accepted it and every later lookup for the
        // controller threw — inside a message handler, killing the join with no reply.
        Assert.False(playerManager.AddPlayer(replacement));

        Assert.True(playerManager.TryGetPlayer(ControllerId, out var resolved));
        Assert.Same(registered, resolved);
        Assert.Same(registered, Assert.Single(playerManager.Players));
    }

    [Fact]
    public void AddPlayer_SameInstanceTwice_IsRefused()
    {
        var playerManager = CreatePlayerManager(out _);
        var player = new Player(ControllerId, string.Empty, string.Empty, string.Empty, string.Empty);

        Assert.True(playerManager.AddPlayer(player));
        Assert.False(playerManager.AddPlayer(player));

        Assert.Same(player, Assert.Single(playerManager.Players));
    }

    [Fact]
    public void AddPlayer_NoControllerId_IsRefused()
    {
        var playerManager = CreatePlayerManager(out _);

        Assert.False(playerManager.AddPlayer(
            new Player(string.Empty, "SomeHero", string.Empty, string.Empty, string.Empty)));

        Assert.Empty(playerManager.Players);
        Assert.False(playerManager.TryGetPlayer(string.Empty, out _));
    }

    [Fact]
    public void RemovePlayer_ThenRegisterReplacement_SucceedsForSameController()
    {
        var playerManager = CreatePlayerManager(out _);
        var stale = new Player(ControllerId, "MissingHero", string.Empty, string.Empty, string.Empty);
        var replacement = new Player(ControllerId, "CreatedHero", string.Empty, string.Empty, string.Empty);

        Assert.True(playerManager.AddPlayer(stale));

        // The join flow deregisters a registration naming a hero that no longer exists before
        // routing its owner to character creation, so the created character can take the id.
        Assert.True(playerManager.RemovePlayer(stale));
        Assert.True(playerManager.AddPlayer(replacement));

        Assert.True(playerManager.TryGetPlayer(ControllerId, out var resolved));
        Assert.Same(replacement, resolved);
        Assert.Same(replacement, Assert.Single(playerManager.Players));
    }

    [Fact]
    public void RemovePlayer_SupersededInstance_LeavesCurrentRegistrationIntact()
    {
        var playerManager = CreatePlayerManager(out _);
        var stale = new Player(ControllerId, "MissingHero", string.Empty, string.Empty, string.Empty);
        var current = new Player(ControllerId, "CreatedHero", string.Empty, string.Empty, string.Empty);

        Assert.True(playerManager.AddPlayer(stale));
        Assert.True(playerManager.RemovePlayer(stale));
        Assert.True(playerManager.AddPlayer(current));

        // A caller still holding the superseded Player must not deregister its successor.
        Assert.False(playerManager.RemovePlayer(stale));

        Assert.True(playerManager.TryGetPlayer(ControllerId, out var resolved));
        Assert.Same(current, resolved);
    }

    [Fact]
    public void ReplacePlayer_CurrentRegistration_UpdatesControllerAndPeerAtomically()
    {
        var playerManager = CreatePlayerManager(out _);
        var registered = new Player(ControllerId, "Hero", "StaleParty", "Clan", "Character");
        var replacement = new Player(ControllerId, "Hero", "RecoveredParty", "Clan", "Character");
        var peer = new TestNetwork().CreatePeer();

        Assert.True(playerManager.AddPlayer(registered));
        playerManager.SetPeer(ControllerId, peer);

        Assert.True(playerManager.ReplacePlayer(registered, replacement));

        Assert.True(playerManager.TryGetPlayer(ControllerId, out var byController));
        Assert.Same(replacement, byController);
        Assert.True(playerManager.TryGetPlayer(peer, out var byPeer));
        Assert.Same(replacement, byPeer);
        Assert.Same(replacement, Assert.Single(playerManager.Players));
    }

    [Fact]
    public void ReplacePlayer_ChangedParty_ReplacesControlledObjectMarker()
    {
        var staleParty = ObjectHelper.SkipConstructor<MobileParty>();
        var recoveredParty = ObjectHelper.SkipConstructor<MobileParty>();
        var playerManager = CreatePlayerManager(out var objectManager);
        var registered = new Player(ControllerId, string.Empty, "StaleParty", string.Empty, string.Empty);
        var replacement = new Player(ControllerId, string.Empty, "RecoveredParty", string.Empty, string.Empty);
        MobileParty resolvedStaleParty = staleParty;
        MobileParty resolvedRecoveredParty = recoveredParty;
        objectManager
            .Setup(manager => manager.TryGetObjectWithLogging("StaleParty", out resolvedStaleParty))
            .Returns(true);
        objectManager
            .Setup(manager => manager.TryGetObject("StaleParty", out resolvedStaleParty))
            .Returns(true);
        objectManager
            .Setup(manager => manager.TryGetObjectWithLogging("RecoveredParty", out resolvedRecoveredParty))
            .Returns(true);

        var playerObjects = GetPlayerObjects();
        try
        {
            Assert.True(playerManager.AddPlayer(registered));
            Assert.True(playerManager.Contains(staleParty));

            Assert.True(playerManager.ReplacePlayer(registered, replacement));

            Assert.False(playerManager.Contains(staleParty));
            Assert.True(playerManager.Contains(recoveredParty));
        }
        finally
        {
            playerObjects.Remove(staleParty);
            playerObjects.Remove(recoveredParty);
        }
    }

    [Fact]
    public void ReplacePlayer_SupersededRegistration_LeavesCurrentRegistrationIntact()
    {
        var playerManager = CreatePlayerManager(out _);
        var superseded = new Player(ControllerId, "OldHero", string.Empty, string.Empty, string.Empty);
        var current = new Player(ControllerId, "CurrentHero", string.Empty, string.Empty, string.Empty);
        var replacement = new Player(ControllerId, "ReplacementHero", string.Empty, string.Empty, string.Empty);

        Assert.True(playerManager.AddPlayer(superseded));
        Assert.True(playerManager.RemovePlayer(superseded));
        Assert.True(playerManager.AddPlayer(current));

        Assert.False(playerManager.ReplacePlayer(superseded, replacement));
        Assert.Same(current, Assert.Single(playerManager.Players));
    }

    private static ConditionalWeakTable<object, ControlledObjectInfo> GetPlayerObjects() =>
        (ConditionalWeakTable<object, ControlledObjectInfo>)AccessTools
            .Field(typeof(PlayerManager), "PlayerObjects")
            .GetValue(null)!;
}
