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
    /// Adds a player to the registry
    /// </summary>
    /// <param name="player">The player to be added to the registry</param>
    /// <returns>if the player was added to the registry</returns>
    bool AddPlayer(Player player);
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
    /// Removes a peer's association, for example, on disconnect. The Player
    /// registration is untouched, only the live peer link is dropped.
    /// </summary>
    void ClearPeer(NetPeer peer);

    /// <summary>
    /// Resolves the Player currently controlled by a connected peer.
    /// </summary>
    bool TryGetPlayer(NetPeer peer, out Player player);

    /// <summary>
    /// Checks whether the given player has a connected peer.
    /// </summary>
    bool IsConnected(Player player);
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

    public IReadOnlyCollection<Player> Players => _players.Values;
    private readonly Dictionary<string, Player> _players = new();

    public PlayerManager(ILogger logger, IObjectManager objectManager, IControllerIdProvider controllerIdProvider)
    {
        this.logger = logger;
        this.objectManager = objectManager;
        this.controllerIdProvider = controllerIdProvider;
    }

    /// <inheritdoc cref="IPlayerManager.AddPlayer(Player)"/>
    public bool AddPlayer(Player player)
    {
        _players[player.ControllerId] = player;

        // Add player objects for IsPlayer extension (i.e. MobilePartyExtensions)
        AddPlayerObject<MobileParty>(player.ControllerId, player.MobilePartyId);
        AddPlayerObject<Hero>(player.ControllerId, player.HeroId);
        AddPlayerObject<Clan>(player.ControllerId, player.ClanId);

        return true;
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
    }

    private void InvalidatePlayerPartySpeedCache(MobileParty mobileParty)
    {
        GameThread.RunSafe(() =>
        {
            mobileParty._partyPureSpeedLastCheckVersion = -1;
        }, context: nameof(PlayerManager));
    }

    public bool TryGetPlayer(string controllerId, out Player player)
    {
        return _players.TryGetValue(controllerId, out player);
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

        peerToPlayer[peer] = player;
    }

    public void ClearPeer(NetPeer peer)
    {
        peerToPlayer.TryRemove(peer, out _);
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