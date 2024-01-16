using Common.Messaging;
using GameInterface.Services.Players.Data;
using GameInterface.Services.Players.Messages;
using System.Collections;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.Players;

/// <summary>
/// Keeps track & managers all players on the server/client. 
/// </summary>
internal interface IPlayerRegistry: IEnumerable<Player>
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
    private readonly IMessageBroker messageBroker;
    private readonly HashSet<Player> _players = new HashSet<Player>();
    private readonly HashSet<string> _playerMobileParties = new HashSet<string>();

    public PlayerRegistry(IMessageBroker messageBroker) {
        this.messageBroker = messageBroker;
    }

    /// <inheritdoc cref="IPlayerRegistry.AddPlayer(Player)"/>
    public bool AddPlayer(Player player)
    {
        if (!_players.Add(player)) return false;

        if (!_playerMobileParties.Add(player.PartyStringId)) return false;
        messageBroker.Publish(this, new PlayerRegistered(player));
        return true;
    }

    /// <inheritdoc cref="IPlayerRegistry.Contains(MobileParty)"/>
    public bool Contains(MobileParty player)
    {
        return _playerMobileParties.Contains(player.StringId);
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
