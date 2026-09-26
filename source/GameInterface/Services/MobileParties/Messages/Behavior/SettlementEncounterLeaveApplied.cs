using Common.Messaging;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;

namespace GameInterface.Services.MobileParties.Messages.Behavior;

public readonly struct SettlementEncounterLeaveApplied : IEvent
{
    public MobileParty Party { get; }
    public Settlement Settlement { get; }

    public SettlementEncounterLeaveApplied(MobileParty party, Settlement settlement)
    {
        Party = party;
        Settlement = settlement;
    }
}
