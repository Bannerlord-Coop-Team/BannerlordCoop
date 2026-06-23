using Common.Messaging;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.Armies.Messages;

/// <summary>
/// Event for when a player adds parties to an existing army via the army management UI
/// </summary>
public readonly struct PlayerAddedPartiesToArmy : IEvent
{
    public readonly Army Army;
    public readonly List<MobileParty> Parties;

    public PlayerAddedPartiesToArmy(Army army, List<MobileParty> parties)
    {
        Army = army;
        Parties = parties;
    }
}