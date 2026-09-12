using Common.Messaging;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;

namespace GameInterface.Services.SiegeEvents.Messages;

/// <summary>
/// The local player completed the break-in debrief and needs the server to enter its party into the settlement.
/// </summary>
public readonly struct BreakInContinuationAttempted : IEvent
{
    public readonly MobileParty Party;
    public readonly Settlement Settlement;

    public BreakInContinuationAttempted(MobileParty party, Settlement settlement)
    {
        Party = party;
        Settlement = settlement;
    }
}
