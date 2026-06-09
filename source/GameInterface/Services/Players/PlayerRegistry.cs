using GameInterface.Services.Entity;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Players.Data;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.Players;

/// <summary>
/// Keeps track & managers all players on the server/client. 
/// </summary>
public interface IPlayerRegistry: IEnumerable<Player>
{
    /// <summary>
    /// Adds a player to the registry
    /// </summary>
    /// <param name="player">The player to be added to the registry</param>
    /// <returns>if the player was added to the registry</returns>
    bool AddPlayer(Player player);
    
    /// <summary>
    /// Checks if the Mobileparty is a player party
    /// </summary>
    /// <param name="mobileParty">checks to see if a <paramref name="mobileParty"/> is a player</param>
    /// <returns>true if the <paramref name="mobileParty"/> is a player otherwise false.</returns>
    bool Contains(MobileParty mobileParty);
}

/// <inheritdoc cref="IPlayerRegistry"/>
internal class PlayerRegistry : IPlayerRegistry
{
    // Key is controlled entity, value is unused so we just initialize it to an object
    public static readonly ConditionalWeakTable<object, object> PlayerObjects = new();

    private readonly IObjectManager objectManager;
    private readonly IControlledEntityRegistry entityRegistry;
    private readonly IControllerIdProvider controllerIdProvider;
    private readonly HashSet<Player> _players = new HashSet<Player>();
    private readonly HashSet<string> _playerMobileParties = new HashSet<string>();

    public PlayerRegistry(IObjectManager objectManager, IControlledEntityRegistry entityRegistry, IControllerIdProvider controllerIdProvider)
    {
        this.objectManager = objectManager;
        this.entityRegistry = entityRegistry;
        this.controllerIdProvider = controllerIdProvider;
    }

    /// <inheritdoc cref="IPlayerRegistry.AddPlayer(Player)"/>
    public bool AddPlayer(Player player)
    {
        if (!_players.Add(player)) return false;

        if (!_playerMobileParties.Add(player.MobilePartyId)) return false;

        // Add player objects for IsPlayer extension (i.e. MobilePartyExtensions)
        AddPlayerObject<MobileParty>(player.MobilePartyId);
        AddPlayerObject<Hero>(player.HeroId);
        AddPlayerObject<Clan>(player.ClanId);
        AddPlayerObject<CharacterObject>(player.CharacterObjectId);

        return true;
    }

    private void AddPlayerObject<T>(string networkId)
    {
        if (!objectManager.TryGetObjectWithLogging<T>(networkId, out var obj))
            return;

        // Sets the value if it does not exist
        PlayerObjects.GetValue(obj, _ => new object());

        entityRegistry.RegisterAsControlled(controllerIdProvider.ControllerId, networkId);
    }

    /// <inheritdoc cref="IPlayerRegistry.Contains(MobileParty)"/>
    public bool Contains(MobileParty player)
    {
        if (!objectManager.TryGetIdWithLogging(player, out var partyId))
            return false;

        return _playerMobileParties.Contains(partyId);
    }

    public IEnumerator<Player> GetEnumerator()
    {
        return _players.GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return _players.GetEnumerator();
    }
}
