using Common.Messaging;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;

namespace GameInterface.Services.SiegeEvents.Messages;

/// <summary>
/// The local player broke into a besieged settlement to reinforce its defenders; ask the server to
/// move the party inside.
/// </summary>
/// <remarks>
/// Unlike the other siege entry points this one cannot wait for approval. Vanilla's
/// break_in_debrief_continue_on_consequence dereferences the hero's CurrentSettlement two
/// instructions after entering, so the client applies the entry locally and reports it, rather than
/// asking first and continuing later.
/// </remarks>
public readonly struct BreakIntoSettlementAttempted : IEvent
{
    public readonly MobileParty Party;
    public readonly Settlement Settlement;

    public BreakIntoSettlementAttempted(MobileParty party, Settlement settlement)
    {
        Party = party;
        Settlement = settlement;
    }
}
