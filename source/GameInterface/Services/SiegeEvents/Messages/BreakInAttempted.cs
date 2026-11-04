using Common.Messaging;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;

namespace GameInterface.Services.SiegeEvents.Messages;

public readonly struct BreakInAttempted : IEvent
{
    public MobileParty Party { get; }
    public Settlement Settlement { get; }

    public BreakInAttempted(MobileParty party, Settlement settlement)
    {
        Party = party;
        Settlement = settlement;
    }
}
