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

[Collection(ModInformationRoleCollection.Name)]
public class PlayerManagerTests
{
    private const string ControllerId = "PlayerOne";
    private const string OtherControllerId = "PlayerTwo";
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
    public void TrySetPeer_RegisteredPlayer_AssociatesPeerWithPlayer()
    {
        var playerManager = CreatePlayerManager(out _);
        var player = new Player(ControllerId, string.Empty, string.Empty, string.Empty, string.Empty);
        var network = new TestNetwork();
        var peer = network.CreatePeer();

        Assert.True(playerManager.AddPlayer(player));

        Assert.True(playerManager.TrySetPeer(player, peer));

        Assert.True(playerManager.TryGetPlayer(peer, out var resolvedPlayer));
        Assert.Same(player, resolvedPlayer);
        Assert.True(playerManager.TryGetPeer(ControllerId, out var resolvedPeer));
        Assert.Same(peer, resolvedPeer);
    }

    // A join checks a registration on the network thread, and a deletion on the game thread can remove it before the peer is bound.
    [Fact]
    public void TrySetPeer_RegistrationRemovedAfterValidation_BindsNothing()
    {
        var playerManager = CreatePlayerManager(out _);
        var validated = new Player(ControllerId, "Hero", string.Empty, string.Empty, string.Empty);
        var peer = new TestNetwork().CreatePeer();
        Assert.True(playerManager.AddPlayer(validated));
        Assert.True(playerManager.RemovePlayer(validated));

        Assert.False(playerManager.TrySetPeer(validated, peer));

        Assert.False(playerManager.TryGetPlayer(peer, out _));
        Assert.False(playerManager.TryGetPeer(ControllerId, out _));
    }

    // An heir swap can replace it instead, and the peer must not land on a registration it wasn't checked against.
    [Fact]
    public void TrySetPeer_RegistrationReplacedAfterValidation_BindsNothing()
    {
        var playerManager = CreatePlayerManager(out _);
        var validated = new Player(ControllerId, "Hero", string.Empty, string.Empty, string.Empty);
        var replacement = new Player(ControllerId, "Heir", string.Empty, string.Empty, string.Empty);
        var peer = new TestNetwork().CreatePeer();
        Assert.True(playerManager.AddPlayer(validated));
        Assert.True(playerManager.ReplacePlayer(validated, replacement));

        Assert.False(playerManager.TrySetPeer(validated, peer));

        Assert.False(playerManager.TryGetPlayer(peer, out _));
        Assert.False(playerManager.TryGetPeer(ControllerId, out _));
    }

    [Fact]
    public void TrySetPeer_ReplacementRegistration_BindsIt()
    {
        var playerManager = CreatePlayerManager(out _);
        var validated = new Player(ControllerId, "Hero", string.Empty, string.Empty, string.Empty);
        var replacement = new Player(ControllerId, "Heir", string.Empty, string.Empty, string.Empty);
        var peer = new TestNetwork().CreatePeer();
        Assert.True(playerManager.AddPlayer(validated));
        Assert.True(playerManager.ReplacePlayer(validated, replacement));

        Assert.True(playerManager.TrySetPeer(replacement, peer));

        Assert.True(playerManager.TryGetPlayer(peer, out var bound));
        Assert.Same(replacement, bound);
        Assert.True(playerManager.TryGetPeer(ControllerId, out var boundPeer));
        Assert.Same(peer, boundPeer);
    }

    [Fact]
    public void ClearPeer_AssociatedPeer_RemovesAssociation()
    {
        var playerManager = CreatePlayerManager(out _);
        var player = new Player(ControllerId, string.Empty, string.Empty, string.Empty, string.Empty);
        var network = new TestNetwork();
        var peer = network.CreatePeer();

        Assert.True(playerManager.AddPlayer(player));
        Assert.True(playerManager.TrySetPeer(player, peer));

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
        Assert.True(playerManager.TrySetPeer(player, firstPeer));
        Assert.True(playerManager.TrySetPeer(player, secondPeer));

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
    public void TryGetCurrentController_CurrentPeer_ReturnsController()
    {
        var playerManager = CreatePlayerManager(out _);
        var peer = new TestNetwork().CreatePeer();

        var player = new Player(ControllerId, string.Empty, string.Empty, string.Empty, string.Empty);
        Assert.True(playerManager.AddPlayer(player));
        Assert.True(playerManager.TrySetPeer(player, peer));

        Assert.True(playerManager.TryGetCurrentController(peer, out var controllerId));
        Assert.Equal(ControllerId, controllerId);
    }

    [Fact]
    public void TryGetCurrentController_StalePeerAfterReconnect_ReturnsFalse()
    {
        var playerManager = CreatePlayerManager(out _);
        var network = new TestNetwork();
        var stalePeer = network.CreatePeer();
        var currentPeer = network.CreatePeer();

        var player = new Player(ControllerId, string.Empty, string.Empty, string.Empty, string.Empty);
        Assert.True(playerManager.AddPlayer(player));
        Assert.True(playerManager.TrySetPeer(player, stalePeer));
        Assert.True(playerManager.TrySetPeer(player, currentPeer));

        Assert.False(playerManager.TryGetCurrentController(stalePeer, out var controllerId));
        Assert.Null(controllerId);
    }

    [Fact]
    public void IsSenderControllingParty_StalePeerAfterReconnect_ReturnsFalse()
    {
        var playerManager = CreatePlayerManager(out _);
        var network = new TestNetwork();
        // Test peers share one endpoint, and peers compare by endpoint, so the old peer gets its own address.
        var stalePeer = network.CreatePeer("10.0.0.8");
        var currentPeer = network.CreatePeer();

        var player = new Player(ControllerId, string.Empty, PartyId, string.Empty, string.Empty);
        Assert.True(playerManager.AddPlayer(player));
        Assert.True(playerManager.TrySetPeer(player, stalePeer));
        Assert.True(playerManager.TrySetPeer(player, currentPeer));

        Assert.False(playerManager.IsSenderControllingParty(stalePeer, PartyId));
        Assert.True(playerManager.IsSenderControllingParty(currentPeer, PartyId));
    }

    [Fact]
    public void SenderHeroChecks_StalePeerAfterReconnect_AreFalse()
    {
        var playerManager = CreatePlayerManager(out var objectManager);
        var hero = ObjectHelper.SkipConstructor<Hero>();
        Resolve(objectManager, HeroId, hero);
        var network = new TestNetwork();
        var stalePeer = network.CreatePeer("10.0.0.8");
        var currentPeer = network.CreatePeer();

        var player = new Player(ControllerId, HeroId, string.Empty, string.Empty, string.Empty);
        Assert.True(playerManager.AddPlayer(player));
        Assert.True(playerManager.TrySetPeer(player, stalePeer));
        Assert.True(playerManager.TrySetPeer(player, currentPeer));

        Assert.False(playerManager.IsSenderControllingHero(stalePeer, HeroId));
        Assert.False(playerManager.TryGetSenderHero(stalePeer, out _));
        Assert.True(playerManager.IsSenderControllingHero(currentPeer, HeroId));
        Assert.True(playerManager.TryGetSenderHero(currentPeer, out var senderHero));
        Assert.Same(hero, senderHero);
    }

    [Fact]
    public void TryGetCurrentController_UnregisteredPeer_ReturnsFalse()
    {
        var playerManager = CreatePlayerManager(out _);

        Assert.False(playerManager.TryGetCurrentController(new TestNetwork().CreatePeer(), out var controllerId));
        Assert.Null(controllerId);
    }

    [Fact]
    public void IsSenderControllingParty_OwnParty_ReturnsTrue()
    {
        var logger = new Mock<ILogger>();
        var playerManager = new PlayerManager(logger.Object, new Mock<IObjectManager>().Object, new ControllerIdProvider());
        var peer = new TestNetwork().CreatePeer();

        var player = new Player(ControllerId, string.Empty, PartyId, string.Empty, string.Empty);
        Assert.True(playerManager.AddPlayer(player));
        Assert.True(playerManager.TrySetPeer(player, peer));

        Assert.True(playerManager.IsSenderControllingParty(peer, PartyId));
        logger.Verify(log => log.Warning(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int?>()), Times.Never());
    }

    [Fact]
    public void IsSenderControllingParty_AnotherParty_ReturnsFalseAndWarnsWithThePeer()
    {
        var logger = new Mock<ILogger>();
        var playerManager = new PlayerManager(logger.Object, new Mock<IObjectManager>().Object, new ControllerIdProvider());
        var peer = new TestNetwork().CreatePeer();

        var player = new Player(ControllerId, string.Empty, PartyId, string.Empty, string.Empty);
        Assert.True(playerManager.AddPlayer(player));
        Assert.True(playerManager.TrySetPeer(player, peer));

        Assert.False(playerManager.IsSenderControllingParty(peer, "PartyTwo"));
        logger.Verify(log => log.Warning(
            "Ignoring a request for party {PartyId} from peer {PeerId}, which does not control it",
            "PartyTwo", (int?)peer.Id), Times.Once());
    }

    [Fact]
    public void IsSenderControllingParty_UnregisteredPeerOrNoPeer_ReturnsFalse()
    {
        var playerManager = CreatePlayerManager(out _);

        Assert.False(playerManager.IsSenderControllingParty(new TestNetwork().CreatePeer("10.0.0.8"), PartyId));
        Assert.False(playerManager.IsSenderControllingParty(new object(), PartyId));
    }

    [Fact]
    public void IsSenderControllingParty_EmptyPartyIdOnBothSides_ReturnsFalse()
    {
        var playerManager = new PlayerManager(new Mock<ILogger>().Object, new Mock<IObjectManager>().Object, new ControllerIdProvider());
        var peer = new TestNetwork().CreatePeer();

        var player = new Player(ControllerId, string.Empty, string.Empty, string.Empty, string.Empty);
        Assert.True(playerManager.AddPlayer(player));
        Assert.True(playerManager.TrySetPeer(player, peer));

        Assert.False(playerManager.IsSenderControllingParty(peer, string.Empty));
        Assert.False(playerManager.IsSenderControllingParty(peer, null));
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

    // Clients mirror the server's registry, so only the server refuses a second controller for one hero.
    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public void AddPlayer_SecondControllerForRegisteredHero_IsRefusedOnServer(bool isServer, bool expectedAdded)
    {
        var wasServer = ModInformation.IsServer;
        var hero = ObjectHelper.SkipConstructor<Hero>();
        var playerManager = CreatePlayerManager(out var objectManager);
        Resolve(objectManager, HeroId, hero);
        try
        {
            ModInformation.IsServer = isServer;
            Assert.True(playerManager.AddPlayer(new Player(ControllerId, HeroId, string.Empty, string.Empty, string.Empty)));

            Assert.Equal(expectedAdded, playerManager.AddPlayer(
                new Player(OtherControllerId, HeroId, string.Empty, string.Empty, string.Empty)));

            Assert.Equal(expectedAdded ? 2 : 1, playerManager.Players.Count);
            Assert.True(PlayerManager.TryGetControlledObjectInfo(hero, out var control));
            Assert.Equal(ControllerId, control.ObjectControllerId);
        }
        finally
        {
            ModInformation.IsServer = wasServer;
            GetPlayerObjects().Remove(hero);
        }
    }

    [Fact]
    public void AddPlayer_SecondControllerForRegisteredParty_IsRefusedOnServer()
    {
        var wasServer = ModInformation.IsServer;
        var party = ObjectHelper.SkipConstructor<MobileParty>();
        var playerManager = CreatePlayerManager(out var objectManager);
        Resolve(objectManager, PartyId, party);
        try
        {
            ModInformation.IsServer = true;
            Assert.True(playerManager.AddPlayer(new Player(ControllerId, string.Empty, PartyId, string.Empty, string.Empty)));

            Assert.False(playerManager.AddPlayer(
                new Player(OtherControllerId, "HeroTwo", PartyId, string.Empty, string.Empty)));

            Assert.Single(playerManager.Players);
            Assert.True(PlayerManager.TryGetControlledObjectInfo(party, out var control));
            Assert.Equal(ControllerId, control.ObjectControllerId);
        }
        finally
        {
            ModInformation.IsServer = wasServer;
            GetPlayerObjects().Remove(party);
        }
    }

    // A player can keep another player's clan as its clan and original clan, so a shared clan is not a hold.
    [Fact]
    public void AddPlayer_SecondControllerSharingAClan_IsAddedOnServer()
    {
        var wasServer = ModInformation.IsServer;
        var clan = ObjectHelper.SkipConstructor<Clan>();
        var playerManager = CreatePlayerManager(out var objectManager);
        Resolve(objectManager, "ClanOne", clan);
        try
        {
            ModInformation.IsServer = true;
            Assert.True(playerManager.AddPlayer(new Player(ControllerId, HeroId, string.Empty, "ClanOne", string.Empty)));

            Assert.True(playerManager.AddPlayer(new Player(OtherControllerId, "HeroTwo", string.Empty, "ClanOne", string.Empty)));

            Assert.Equal(2, playerManager.Players.Count);
        }
        finally
        {
            ModInformation.IsServer = wasServer;
            GetPlayerObjects().Remove(clan);
        }
    }

    [Fact]
    public void ReplacePlayer_OntoHeroHeldByAnotherController_IsRefusedOnServer()
    {
        var wasServer = ModInformation.IsServer;
        var heldHero = ObjectHelper.SkipConstructor<Hero>();
        var ownHero = ObjectHelper.SkipConstructor<Hero>();
        var playerManager = CreatePlayerManager(out var objectManager);
        Resolve(objectManager, HeroId, heldHero);
        Resolve(objectManager, "HeroTwo", ownHero);
        var other = new Player(OtherControllerId, "HeroTwo", string.Empty, string.Empty, string.Empty);
        try
        {
            ModInformation.IsServer = true;
            Assert.True(playerManager.AddPlayer(new Player(ControllerId, HeroId, string.Empty, string.Empty, string.Empty)));
            Assert.True(playerManager.AddPlayer(other));

            Assert.False(playerManager.ReplacePlayer(
                other, new Player(OtherControllerId, HeroId, string.Empty, string.Empty, string.Empty)));

            Assert.True(playerManager.TryGetPlayer(OtherControllerId, out var resolved));
            Assert.Same(other, resolved);
            Assert.True(PlayerManager.TryGetControlledObjectInfo(heldHero, out var heldControl));
            Assert.Equal(ControllerId, heldControl.ObjectControllerId);
            Assert.True(PlayerManager.TryGetControlledObjectInfo(ownHero, out var ownControl));
            Assert.Equal(OtherControllerId, ownControl.ObjectControllerId);
        }
        finally
        {
            ModInformation.IsServer = wasServer;
            GetPlayerObjects().Remove(heldHero);
            GetPlayerObjects().Remove(ownHero);
        }
    }

    [Fact]
    public void AddPlayer_HeroMarkedForUnregisteredController_TakesTheMarker()
    {
        var wasServer = ModInformation.IsServer;
        var hero = ObjectHelper.SkipConstructor<Hero>();
        var playerManager = CreatePlayerManager(out var objectManager);
        Resolve(objectManager, HeroId, hero);
        GetPlayerObjects().Add(hero, new ControlledObjectInfo("GoneController", new ControllerIdProvider()));
        try
        {
            ModInformation.IsServer = true;
            Assert.True(playerManager.AddPlayer(new Player(ControllerId, HeroId, string.Empty, string.Empty, string.Empty)));

            Assert.True(PlayerManager.TryGetControlledObjectInfo(hero, out var control));
            Assert.Equal(ControllerId, control.ObjectControllerId);
        }
        finally
        {
            ModInformation.IsServer = wasServer;
            GetPlayerObjects().Remove(hero);
        }
    }

    [Fact]
    public void RemovePlayer_OfAControllerThatNeverHeldTheHero_KeepsTheHoldersMarker()
    {
        var wasServer = ModInformation.IsServer;
        var hero = ObjectHelper.SkipConstructor<Hero>();
        var playerManager = CreatePlayerManager(out var objectManager);
        Resolve(objectManager, HeroId, hero);
        var other = new Player(OtherControllerId, HeroId, string.Empty, string.Empty, string.Empty);
        try
        {
            // A client keeps a second registration for a held hero (logged only), but the marker stays with the holder.
            ModInformation.IsServer = false;
            Assert.True(playerManager.AddPlayer(new Player(ControllerId, HeroId, string.Empty, string.Empty, string.Empty)));
            Assert.True(playerManager.AddPlayer(other));

            Assert.True(playerManager.RemovePlayer(other));

            Assert.True(PlayerManager.TryGetControlledObjectInfo(hero, out var control));
            Assert.Equal(ControllerId, control.ObjectControllerId);
        }
        finally
        {
            ModInformation.IsServer = wasServer;
            GetPlayerObjects().Remove(hero);
        }
    }

    [Theory]
    [InlineData(true, "Hero", 40f)]
    [InlineData(true, "Heir", 5f)]
    [InlineData(false, "Hero", 5f)]
    public void ReplacePlayer_PreservesServerCrimeForSameHeroOnly(bool isServer, string heroId, float expected)
    {
        var wasServer = ModInformation.IsServer;
        try
        {
            ModInformation.IsServer = isServer;
            var manager = CreatePlayerManager(out _);
            var original = new Player(ControllerId, "Hero", "Party", "Clan", "Character");
            original.CrimeRatings["Kingdom_empire_s"] = 40f;
            var replacement = new Player(ControllerId, heroId, "NewParty", "Clan", "Character");
            replacement.CrimeRatings["Kingdom_empire_s"] = 5f;
            Assert.True(manager.AddPlayer(original));
            Assert.True(manager.ReplacePlayer(original, replacement));
            Assert.Equal(expected, replacement.CrimeRatings["Kingdom_empire_s"]);
        }
        finally
        {
            ModInformation.IsServer = wasServer;
        }
    }

    // Replacing campaign objects preserves the same controller's peer and saved platform name.
    [Fact]
    public void ReplacePlayer_CurrentRegistration_UpdatesControllerAndPeerAtomically()
    {
        var playerManager = CreatePlayerManager(out _);
        var registered = new Player(ControllerId, "Hero", "StaleParty", "Clan", "Character")
        {
            PlatformName = "Platform player"
        };
        var replacement = new Player(ControllerId, "Hero", "RecoveredParty", "Clan", "Character");
        var peer = new TestNetwork().CreatePeer();

        Assert.True(playerManager.AddPlayer(registered));
        Assert.True(playerManager.TrySetPeer(registered, peer));

        Assert.True(playerManager.ReplacePlayer(registered, replacement));

        Assert.True(playerManager.TryGetPlayer(ControllerId, out var byController));
        Assert.Same(replacement, byController);
        Assert.True(playerManager.TryGetPlayer(peer, out var byPeer));
        Assert.Same(replacement, byPeer);
        Assert.Same(replacement, Assert.Single(playerManager.Players));
        Assert.Equal("Platform player", replacement.PlatformName);
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
    public void ReplacePlayer_CoopClan_KeepsOriginalClanControlled()
    {
        var manager = CreatePlayerManager(out var objects);
        var originalClan = ObjectHelper.SkipConstructor<Clan>();
        var coopClan = ObjectHelper.SkipConstructor<Clan>();
        objects.Setup(value => value.TryGetObject("original-clan", out originalClan)).Returns(true);
        objects.Setup(value => value.TryGetObjectWithLogging("original-clan", out originalClan)).Returns(true);
        objects.Setup(value => value.TryGetObject("coop-clan", out coopClan)).Returns(true);
        objects.Setup(value => value.TryGetObjectWithLogging("coop-clan", out coopClan)).Returns(true);
        var registered = new Player(ControllerId, HeroId, PartyId, "original-clan", "character");
        var restored = new Player(ControllerId, HeroId, PartyId, "coop-clan", "character", registered.OriginalClanId);
        try
        {
            Assert.True(manager.AddPlayer(registered));
            Assert.True(manager.ReplacePlayer(registered, restored));
            Assert.True(manager.Contains(originalClan));
            Assert.False(manager.Contains(coopClan));
            Assert.True(manager.RemovePlayer(restored));
            Assert.False(manager.Contains(originalClan));
        }
        finally
        {
            manager.RemovePlayer(restored);
            manager.RemovePlayer(registered);
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

    [Fact]
    public void RemovingOriginalOwnerKeepsClanControlledByAppointedPlayerLeader()
    {
        var manager = CreatePlayerManager(out var objects);
        var clan = ObjectHelper.SkipConstructor<Clan>();
        var oldHero = ObjectHelper.SkipConstructor<Hero>();
        var newLeader = ObjectHelper.SkipConstructor<Hero>();
        oldHero.OwnedCaravans = new();
        newLeader.OwnedCaravans = new();
        objects.Setup(value => value.TryGetObjectWithLogging("clan", out clan)).Returns(true);
        objects.Setup(value => value.TryGetObject("clan", out clan)).Returns(true);
        objects.Setup(value => value.TryGetObjectWithLogging("old-hero", out oldHero)).Returns(true);
        objects.Setup(value => value.TryGetObject("old-hero", out oldHero)).Returns(true);
        objects.Setup(value => value.TryGetObjectWithLogging("new-leader", out newLeader)).Returns(true);
        objects.Setup(value => value.TryGetObject("new-leader", out newLeader)).Returns(true);
        var original = new Player("original", "old-hero", "", "clan", "");
        var successor = new Player("successor", "new-leader", "", "clan", "", "other-clan");
        try
        {
            clan._leader = oldHero;
            Assert.True(manager.AddPlayer(original));
            Assert.True(manager.AddPlayer(successor));
            clan._leader = newLeader;

            Assert.True(manager.RemovePlayer(original));

            Assert.True(manager.Contains(clan));
            Assert.True(PlayerManager.TryGetControlledObjectInfo(clan, out var control));
            Assert.Equal("successor", control.ObjectControllerId);
        }
        finally
        {
            manager.RemovePlayer(original);
            manager.RemovePlayer(successor);
        }
    }

    [Fact]
    public void TryGetSenderHero_ResolvesTheHeroOfThePeerThatSentTheMessage()
    {
        var playerManager = CreatePlayerManager(out var objectManager);
        var hero = ObjectHelper.SkipConstructor<Hero>();
        Resolve(objectManager, HeroId, hero);
        var peer = new TestNetwork().CreatePeer();

        var player = new Player(ControllerId, HeroId, string.Empty, string.Empty, string.Empty);
        Assert.True(playerManager.AddPlayer(player));
        Assert.True(playerManager.TrySetPeer(player, peer));

        try
        {
            Assert.True(playerManager.TryGetSenderHero(peer, out var senderHero));
            Assert.Same(hero, senderHero);
        }
        finally
        {
            GetPlayerObjects().Remove(hero);
        }
    }

    [Fact]
    public void TryGetSenderHero_UnknownSender_IsFalse()
    {
        var playerManager = CreatePlayerManager(out _);

        Assert.False(playerManager.TryGetSenderHero(new TestNetwork().CreatePeer(), out var unknownPeerHero));
        Assert.Null(unknownPeerHero);
        Assert.False(playerManager.TryGetSenderHero(new object(), out var notAPeerHero));
        Assert.Null(notAPeerHero);
    }

    // Registration resolves with logging, the hold check and RemovePlayer resolve without it.
    private static void Resolve<T>(Mock<IObjectManager> objectManager, string id, T obj)
    {
        objectManager.Setup(o => o.TryGetObject(id, out obj)).Returns(true);
        objectManager.Setup(o => o.TryGetObjectWithLogging(id, out obj)).Returns(true);
    }

    private static ConditionalWeakTable<object, ControlledObjectInfo> GetPlayerObjects() =>
        (ConditionalWeakTable<object, ControlledObjectInfo>)AccessTools
            .Field(typeof(PlayerManager), "PlayerObjects")
            .GetValue(null)!;
}
