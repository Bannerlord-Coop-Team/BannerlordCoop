using Common.Messaging;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;

namespace GameInterface.Services.SiegeEvents.Messages;

/// <summary>
/// The local player chose to besiege a settlement; ask the server to start the siege.
/// </summary>
public readonly struct BesiegeSettlementAttempted : IEvent
{
    public readonly MobileParty Party;
    public readonly Settlement Settlement;

    public BesiegeSettlementAttempted(MobileParty party, Settlement settlement)
    {
        Party = party;
        Settlement = settlement;
    }
}
