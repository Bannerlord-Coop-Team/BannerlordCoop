using Common.Messaging;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.Armies.Messages;

/// <summary>
/// Event for when a player removes parties from an army via the army management UI
/// </summary>
public readonly struct PlayerRemovedPartiesFromArmy : IEvent
{
    public readonly List<MobileParty> Parties;

    public PlayerRemovedPartiesFromArmy(List<MobileParty> parties)
    {
        Parties = parties;
    }
}