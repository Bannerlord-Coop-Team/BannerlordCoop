using Common.Caching;
using Common.Messaging;
using GameInterface.Services.MobileParties.Extensions;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Players.Data;
using GameInterface.Services.Players.Messages;
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
    public static readonly ConditionalWeakTable<object, Player> PlayerObjects = new();

    private readonly IMessageBroker messageBroker;
    private readonly IObjectManager objectManager;
    private readonly HashSet<Player> _players = new HashSet<Player>();
    private readonly HashSet<string> _playerMobileParties = new HashSet<string>();

    public PlayerRegistry(IMessageBroker messageBroker, IObjectManager objectManager) {
        this.messageBroker = messageBroker;
        this.objectManager = objectManager;
    }

    /// <inheritdoc cref="IPlayerRegistry.AddPlayer(Player)"/>
    public bool AddPlayer(Player player)
    {
        if (!_players.Add(player)) return false;

        if (!_playerMobileParties.Add(player.MobilePartyId)) return false;

        // Add player objects for IsPlayer extension (i.e. MobilePartyExtensions)
        AddPlayerObject<MobileParty>(player.MobilePartyId, player);
        AddPlayerObject<Hero>(player.HeroId, player);

        messageBroker.Publish(this, new PlayerRegistered(player));

        return true;
    }

    private void AddPlayerObject<T>(string networkId, Player player)
    {
        if (!objectManager.TryGetObjectWithLogging<T>(networkId, out var obj))
            return;

        PlayerObjects.Add(obj, player);
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
