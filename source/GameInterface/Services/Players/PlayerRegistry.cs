using Common;
using Common.Messaging;
using GameInterface.Services.Players.Data;
using GameInterface.Services.Players.Messages;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.Players;

internal interface IPlayerRegistry: IEnumerable<Player>
{
    bool AddPlayer(Player player);
    
    bool Contains(MobileParty mobileParty);
}
internal class PlayerRegistry : IPlayerRegistry
{
    private readonly IMessageBroker messageBroker;
    private readonly HashSet<Player> _players = new HashSet<Player>();
    private readonly HashSet<string> _playerMobileParties = new HashSet<string>();

    public PlayerRegistry(IMessageBroker messageBroker) {
        this.messageBroker = messageBroker;
    }

    public bool AddPlayer(Player player)
    {
        if (!_players.Add(player)) return false;

        if (!_playerMobileParties.Add(player.PartyStringId)) return false;
        messageBroker.Publish(this, new PlayerRegistered(player));
        return true;
    }

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
