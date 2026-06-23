using Common.Messaging;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;

namespace GameInterface.Services.Armies.Messages;

/// <summary>
/// Event for when a player creates an army via the army management UI
/// </summary>
public readonly struct PlayerCreatedArmy : IEvent
{
    public readonly Kingdom Kingdom;
    public readonly Hero Leader;
    public readonly Settlement TargetSettlement;
    public readonly Army.ArmyTypes ArmyType;
    public readonly List<MobileParty> Parties;

    public PlayerCreatedArmy(Kingdom kingdom, Hero leader, Settlement targetSettlement, Army.ArmyTypes armyType, List<MobileParty> parties)
    {
        Kingdom = kingdom;
        Leader = leader;
        TargetSettlement = targetSettlement;
        ArmyType = armyType;
        Parties = parties;
    }
}