using GameInterface.Services.Entity;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Players.Data;
using Serilog;
using System.Collections;
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
}

/// <inheritdoc cref="IPlayerManager"/>
public class PlayerManager : IPlayerManager
{
    // Key is controlled entity, value is control info
    private static readonly ConditionalWeakTable<object, ControlledObjectInfo> PlayerObjects = new();
    private readonly ILogger logger;
    private readonly IObjectManager objectManager;
    private readonly IControllerIdProvider controllerIdProvider;

    public IReadOnlyCollection<Player> Players => _players;
    private readonly HashSet<Player> _players = new HashSet<Player>();

    public PlayerManager(ILogger logger, IObjectManager objectManager, IControllerIdProvider controllerIdProvider)
    {
        this.logger = logger;
        this.objectManager = objectManager;
        this.controllerIdProvider = controllerIdProvider;
    }

    /// <inheritdoc cref="IPlayerManager.AddPlayer(Player)"/>
    public bool AddPlayer(Player player)
    {
        if (!_players.Add(player)) return false;

        // Add player objects for IsPlayer extension (i.e. MobilePartyExtensions)
        AddPlayerObject<MobileParty>(player.ControllerId, player.MobilePartyId);
        AddPlayerObject<Hero>(player.ControllerId, player.HeroId);
        AddPlayerObject<Clan>(player.ControllerId, player.ClanId);

        return true;
    }

    private void AddPlayerObject<T>(string controllerId, string networkId)
    {
        if (!objectManager.TryGetObjectWithLogging<T>(networkId, out var obj))
            return;

        // Sets the value if it does not exist
        if (PlayerObjects.TryGetValue(obj, out var _))
        {
            logger.Error("{objType} was already added to {field}", obj.GetType(), nameof(PlayerObjects));
            return;
        }

        PlayerObjects.Add(obj, new ControlledObjectInfo(controllerId, controllerIdProvider));
    }

    public bool TryGetPlayer(string controllerId, out Player player)
    {
        player = _players.SingleOrDefault(player => player.ControllerId == controllerId);

        return player != null;
    }

    public static bool TryGetControlledObjectInfo(object obj, out ControlledObjectInfo info)
    {
        return PlayerObjects.TryGetValue(obj, out info);
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