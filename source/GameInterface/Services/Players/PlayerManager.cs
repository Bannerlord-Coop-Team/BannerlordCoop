using Common;
using GameInterface.Services.Entity;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Players.Data;
using LiteNetLib;
using Serilog;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.Players;

/// <summary>
/// Keeps track & managers all players on the server/client. 
/// </summary>
public interface IPlayerManager
{
    IReadOnlyCollection<Player> Players { get; }

    /// <summary>
    /// Adds a player to the registry. A controller id maps to exactly one player, so a
    /// registration for a controller that already has one is refused; deregister the old one
    /// first (<see cref="RemovePlayer"/>) when replacing it.
    /// </summary>
    /// <param name="player">The player to be added to the registry</param>
    /// <returns>if the player was added to the registry</returns>
    bool AddPlayer(Player player);
    bool ReplacePlayer(Player registeredPlayer, Player replacementPlayer);
    bool TryGetPlayer(string controllerId, out Player player);

    /// <summary>
    /// Checks whether the given game object (hero, party, clan) belongs to a registered player.
    /// </summary>
    /// <param name="obj">The game object to look up</param>
    /// <returns>true if the object is player controlled</returns>
    bool Contains(object obj);

    /// <summary>
    /// Associates a connected peer with the (already registered) player behind
    /// controllerId. Call once the peer's identity is known: on first character creation and
    /// on every reconnect, since a rejoin gets a new NetPeer.
    /// </summary>
    void SetPeer(string controllerId, NetPeer peer);

    /// <summary>
    /// Resolves the currently associated peer for a registered controller.
    /// </summary>
    bool TryGetPeer(string controllerId, out NetPeer peer);

    /// <summary>
    /// Removes a peer's association, for example, on disconnect. The Player
    /// registration is untouched, only the live peer link is dropped.
    /// </summary>
    void ClearPeer(NetPeer peer);

    /// <summary>
    /// Deletes a player's registration entirely: the Player entry, every peer link, and the
    /// controlled-object markers on its hero/party/clan. The game objects themselves are
    /// untouched. Used when a player's character is deleted; afterwards a rejoin with the same
    /// controller id goes through character creation again.
    /// </summary>
    /// <param name="player">The player to delete from the registry</param>
    /// <returns>true if the player was registered and is now removed</returns>
    bool RemovePlayer(Player player);

    /// <summary>
    /// Resolves the Player currently controlled by a connected peer.
    /// </summary>
    bool TryGetPlayer(NetPeer peer, out Player player);

    /// <summary>
    /// Checks whether the given player has a connected peer.
    /// </summary>
    bool IsConnected(Player player);

    /// <summary>
    /// Checks whether the given mobileParty's owner is disconnected.
    /// </summary>
    bool IsOwnerOfPartyDisconnected(MobileParty party);

    /// <summary>
    /// Checks whether the given hero's owner is disconnected.
    /// </summary>
    bool IsOwnerOfHeroDisconnected(Hero hero);
}

/// <inheritdoc cref="IPlayerManager"/>
public class PlayerManager : IPlayerManager
{
    // Key is controlled entity, value is control info
    private static readonly ConditionalWeakTable<object, ControlledObjectInfo> PlayerObjects = new();
    private readonly ILogger logger;
    private readonly IObjectManager objectManager;
    private readonly IControllerIdProvider controllerIdProvider;
    private readonly ConcurrentDictionary<NetPeer, Player> peerToPlayer = new();
    private readonly Dictionary<string, NetPeer> controllerToPeer = new();

    // Guards _players and controllerToPeer: registrations mutate on the game thread (e.g. a
    // player deletion) while join handlers read them on the network thread.
    private readonly object registrySync = new();

    public IReadOnlyCollection<Player> Players
    {
        get
        {
            lock (registrySync)
            {
                return _players.Values.ToArray();
            }
        }
    }

    // Keyed by controller id so a controller can only ever resolve to one player. Player has no
    // value equality, so a set of instances happily accepted a second registration for a
    // controller that already had one: a registration whose hero no longer existed sent its owner
    // back through character creation, the created character registered the same controller
    // again, and every later lookup for it threw inside a message handler — killing the join with
    // no reply and leaving that client on the validation screen until its deadline expired.
    private readonly Dictionary<string, Player> _players = new Dictionary<string, Player>();

    public PlayerManager(ILogger logger, IObjectManager objectManager, IControllerIdProvider controllerIdProvider)
    {
        this.logger = logger;
        this.objectManager = objectManager;
        this.controllerIdProvider = controllerIdProvider;
    }

    /// <inheritdoc cref="IPlayerManager.AddPlayer(Player)"/>
    public bool AddPlayer(Player player)
    {
        if (player == null) return false;

        if (string.IsNullOrEmpty(player.ControllerId))
        {
            logger.Error("Refusing to register a player with no controller id (hero {HeroId})", player.HeroId);
            return false;
        }

        lock (registrySync)
        {
            if (_players.TryGetValue(player.ControllerId, out var registered))
            {
                // Keep the existing registration rather than replacing it silently: the caller
                // owns that decision, and the join flow already deregisters a dead registration
                // before creating a replacement. Reaching this means an unexpected double-add, or
                // a save written before this fix that carries two entries for one controller.
                if (!ReferenceEquals(registered, player))
                    logger.Error(
                        "Controller {ControllerId} is already registered to hero {RegisteredHeroId}; " +
                        "refusing to also register hero {HeroId}",
                        player.ControllerId, registered.HeroId, player.HeroId);

                return false;
            }

            _players.Add(player.ControllerId, player);
        }

        // Add player objects for IsPlayer extension (i.e. MobilePartyExtensions)
        AddPlayerObject<MobileParty>(player.ControllerId, player.MobilePartyId);
        AddPlayerObject<Hero>(player.ControllerId, player.HeroId);
        AddPlayerObject<Clan>(player.ControllerId, player.ClanId);

        return true;
    }

    public bool ReplacePlayer(Player registeredPlayer, Player replacementPlayer)
    {
        if (registeredPlayer == null || replacementPlayer == null ||
            string.IsNullOrEmpty(registeredPlayer.ControllerId) ||
            registeredPlayer.ControllerId != replacementPlayer.ControllerId)
            return false;

        lock (registrySync)
        {
            if (!_players.TryGetValue(registeredPlayer.ControllerId, out var current) ||
                !ReferenceEquals(current, registeredPlayer))
                return false;

            _players[registeredPlayer.ControllerId] = replacementPlayer;

            foreach (var peer in peerToPlayer
                .Where(kvp => ReferenceEquals(kvp.Value, registeredPlayer))
                .Select(kvp => kvp.Key).ToArray())
            {
                peerToPlayer[peer] = replacementPlayer;
            }
        }

        ReplacePlayerObject<MobileParty>(registeredPlayer.ControllerId, registeredPlayer.MobilePartyId, replacementPlayer.MobilePartyId);
        ReplacePlayerObject<Hero>(registeredPlayer.ControllerId, registeredPlayer.HeroId, replacementPlayer.HeroId);
        ReplacePlayerObject<Clan>(registeredPlayer.ControllerId, registeredPlayer.ClanId, replacementPlayer.ClanId);
        return true;
    }

    private void ReplacePlayerObject<T>(string controllerId, string oldId, string newId)
    {
        if (oldId == newId) return;

        RemovePlayerObject<T>(oldId);
        AddPlayerObject<T>(controllerId, newId);
    }

    private void AddPlayerObject<T>(string controllerId, string networkId)
    {
        // Not every player has every object (e.g. no clan yet)
        if (string.IsNullOrEmpty(networkId))
            return;

        if (!objectManager.TryGetObjectWithLogging<T>(networkId, out var obj))
            return;

        // Sets the value if it does not exist
        if (PlayerObjects.TryGetValue(obj, out var _))
        {
            logger.Error("{objType} was already added to {field}", obj.GetType(), nameof(PlayerObjects));
            return;
        }

        PlayerObjects.Add(obj, new ControlledObjectInfo(controllerId, controllerIdProvider));

        if (obj is MobileParty mobileParty)
        {
            InvalidatePlayerPartySpeedCache(mobileParty);
        }

        if (obj is Hero hero)
        {
            InvalidatePlayerCaravanMemberLimitCaches(hero);
        }
    }

    private void InvalidatePlayerPartySpeedCache(MobileParty mobileParty)
    {
        GameThread.RunSafe(() =>
        {
            mobileParty._partyPureSpeedLastCheckVersion = -1;
        }, context: nameof(PlayerManager));
    }

    private void InvalidatePlayerCaravanMemberLimitCaches(Hero hero)
    {
        GameThread.RunSafe(() =>
        {
            if (hero.OwnedCaravans == null) return;

            foreach (var ownedCaravan in hero.OwnedCaravans)
            {
                ownedCaravan.Party._partyMemberSizeLastCheckVersion = -1;
            }
        }, context: nameof(PlayerManager));
    }

    public bool TryGetPlayer(string controllerId, out Player player)
    {
        player = null;

        if (string.IsNullOrEmpty(controllerId)) return false;

        lock (registrySync)
        {
            return _players.TryGetValue(controllerId, out player);
        }
    }

    /// <inheritdoc cref="IPlayerManager.Contains(object)"/>
    public bool Contains(object obj)
    {
        return obj != null && PlayerObjects.TryGetValue(obj, out _);
    }

    public static bool TryGetControlledObjectInfo(object obj, out ControlledObjectInfo info)
    {
        return PlayerObjects.TryGetValue(obj, out info);
    }
    public void SetPeer(string controllerId, NetPeer peer)
    {
        if (!TryGetPlayer(controllerId, out var player))
        {
            logger.Error("Cannot associate peer with unregistered controller {ControllerId}", controllerId);
            return;
        }

        lock (registrySync)
        {
            peerToPlayer[peer] = player;
            controllerToPeer[controllerId] = peer;
        }
    }

    public bool TryGetPeer(string controllerId, out NetPeer peer)
    {
        lock (registrySync)
        {
            return controllerToPeer.TryGetValue(controllerId, out peer);
        }
    }

    public void ClearPeer(NetPeer peer)
    {
        lock (registrySync)
        {
            if (!peerToPlayer.TryRemove(peer, out var player)) return;

            if (controllerToPeer.TryGetValue(player.ControllerId, out var currentPeer) &&
                ReferenceEquals(currentPeer, peer))
                controllerToPeer.Remove(player.ControllerId);
        }
    }

    /// <inheritdoc cref="IPlayerManager.RemovePlayer(Player)"/>
    public bool RemovePlayer(Player player)
    {
        if (player == null || string.IsNullOrEmpty(player.ControllerId)) return false;

        lock (registrySync)
        {
            // Match the instance, not just the controller id: a caller holding a superseded
            // Player must not deregister whoever currently holds that controller.
            if (!_players.TryGetValue(player.ControllerId, out var registered) ||
                !ReferenceEquals(registered, player))
                return false;

            _players.Remove(player.ControllerId);
            controllerToPeer.Remove(player.ControllerId);

            // A rejoin adds a fresh peer link without clearing the old one, so sweep every peer
            // still mapped to this player, not just the current one.
            foreach (var stalePeer in peerToPlayer
                         .Where(kvp => ReferenceEquals(kvp.Value, player))
                         .Select(kvp => kvp.Key).ToArray())
            {
                peerToPlayer.TryRemove(stalePeer, out _);
            }
        }

        RemovePlayerObject<MobileParty>(player.MobilePartyId);
        RemovePlayerObject<Hero>(player.HeroId);
        RemovePlayerObject<Clan>(player.ClanId);

        return true;
    }

    private void RemovePlayerObject<T>(string networkId)
    {
        if (string.IsNullOrEmpty(networkId)) return;

        // The object may already be gone (e.g. a destroyed party); nothing to unmark then.
        if (!objectManager.TryGetObject<T>(networkId, out var obj)) return;

        PlayerObjects.Remove(obj);

        if (obj is MobileParty mobileParty)
        {
            InvalidatePlayerPartySpeedCache(mobileParty);
        }
    }

    public bool TryGetPlayer(NetPeer peer, out Player player)
    {
        return peerToPlayer.TryGetValue(peer, out player);
    }
    public bool IsConnected(Player player)
    {
        return peerToPlayer.Any(kvp =>
         kvp.Value == player && kvp.Key.ConnectionState == ConnectionState.Connected);
    }

    public bool IsOwnerOfPartyDisconnected(MobileParty party) =>
        Players.Any(player =>
            !IsConnected(player) &&
            objectManager.TryGetObject<MobileParty>(player.MobilePartyId, out var playerParty) &&
            ReferenceEquals(playerParty, party));

    public bool IsOwnerOfHeroDisconnected(Hero hero) =>
        Players.Any(player =>
            !IsConnected(player) &&
            objectManager.TryGetObject<Hero>(player.HeroId, out var playerHero) &&
            ReferenceEquals(playerHero, hero));
}

public class ControlledObjectInfo
{
    public readonly string ObjectControllerId;
    public readonly IControllerIdProvider ControllerIdProvider;

    public ControlledObjectInfo(string controllerId, IControllerIdProvider controllerIdProvider)
    {
        ObjectControllerId = controllerId;
        ControllerIdProvider = controllerIdProvider;
    }

    public bool IsControlled => ObjectControllerId == ControllerIdProvider.ControllerId;
}
