using Common.Messaging;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;

namespace GameInterface.Services.MapEvents.Messages.Retreat;

/// <summary>
/// The local player broke into a besieged settlement; ask the server to apply the losses.
/// </summary>
public readonly struct BreakInCasualtiesAttempted : IEvent
{
    public readonly MobileParty Party;
    public readonly Settlement Settlement;

    public BreakInCasualtiesAttempted(MobileParty party, Settlement settlement)
    {
        Party = party;
        Settlement = settlement;
    }
}
