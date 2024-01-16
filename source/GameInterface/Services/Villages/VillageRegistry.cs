using Common;
using Common.Messaging;
using GameInterface.Services.Clans;
using GameInterface.Services.Players.Data;
using GameInterface.Services.Players.Messages;
using GameInterface.Services.Registry;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;

namespace GameInterface.Services.Players;

/// <summary>
/// Keeps track & managers all players on the server/client. 
/// </summary>
internal interface IVillageRegistry : IRegistry<Village>
{
    /// <summary>
    /// Adds a player to the registry
    /// </summary>
    /// <param name="player">The player to be added to the registry</param>
    /// <returns>if the player was added to the registry</returns>
    bool RegisterVillage(Village village);

    /// <summary>
    /// Checks if the Mobileparty is a player party
    /// </summary>
    /// <param name="mobileParty">checks to see if a <paramref name="mobileParty"/> is a player</param>
    /// <returns>true if the <paramref name="mobileParty"/> is a player otherwise false.</returns>
    bool Contains(Village village);

    bool UpdateVillageState(Village village);

    void RegisterAllVillages();
}
/// <inheritdoc cref="IPlayerRegistry"/>
internal class VillageRegistry : RegistryBase<Village>, IVillageRegistry
{
    private readonly IMessageBroker messageBroker;
    private readonly HashSet<Player> _players = new HashSet<Player>();
    private readonly HashSet<string> _playerMobileParties = new HashSet<string>();

    public VillageRegistry(IMessageBroker messageBroker)
    {
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

    public bool AddVillage(Village village)
    {
        throw new System.NotImplementedException();
    }

    /// <inheritdoc cref="IPlayerRegistry.Contains(MobileParty)"/>
    public bool Contains(MobileParty player)
    {
        return _playerMobileParties.Contains(player.StringId);
    }

    public bool Contains(Village village)
    {
        throw new System.NotImplementedException();
    }
    public void RegisterAllVillages()
    {
        var objectManager = Campaign.Current?.CampaignObjectManager;

        if (objectManager == null)
        {
            Logger.Error("Unable to register objects when CampaignObjectManager is null");
            return;
        }

        foreach (var settlement in objectManager.Settlements)
        {
            if(settlement.IsVillage)
            {
                RegisterVillage(settlement.Village);
            }
        }
    }

    public override bool RegisterNewObject(Village obj, out string id)
    {
        throw new System.NotImplementedException();
    }

    public bool RegisterVillage(Village village)
    {
        throw new System.NotImplementedException();
    }

    public bool UpdateVillageState(Village village)
    {
        throw new System.NotImplementedException();
    }


}
