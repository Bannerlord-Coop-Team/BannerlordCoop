using Common.Messaging;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;

namespace GameInterface.Services.Settlements.Messages;

public readonly struct ClanMembersAssignedToSettlement : IEvent
{
    public readonly Settlement Settlement;
    public readonly MobileParty MainParty;
    public readonly List<Hero> LeftHeroes;

    public ClanMembersAssignedToSettlement(Settlement settlement, MobileParty mainParty, List<Hero> leftHeroes)
    {
        Settlement = settlement;
        MainParty = mainParty;
        LeftHeroes = leftHeroes;
    }
}
