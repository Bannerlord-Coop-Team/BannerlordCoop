using Common.Messaging;
using GameInterface.Services.Villages.Data;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;

namespace GameInterface.Services.Villages.Messages;

public readonly struct VillageHostileActionAttempted : IEvent
{
    public readonly VillageHostileAction Action;
    public readonly MobileParty MobileParty;
    public readonly Settlement Settlement;
    public readonly bool FinishEncounterOnDenied;

    public VillageHostileActionAttempted(
        VillageHostileAction action,
        MobileParty mobileParty,
        Settlement settlement,
        bool finishEncounterOnDenied = false)
    {
        Action = action;
        MobileParty = mobileParty;
        Settlement = settlement;
        FinishEncounterOnDenied = finishEncounterOnDenied;
    }
}
