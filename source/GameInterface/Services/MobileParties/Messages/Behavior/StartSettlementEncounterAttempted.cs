using Common.Messaging;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;

namespace GameInterface.Services.MobileParties.Messages.Behavior;

/// <summary>
/// Triggered when a player attempts to enter a settlement.
/// </summary>
public readonly struct StartSettlementEncounterAttempted : IEvent
{
    public readonly MobileParty Party;
    public readonly Settlement Settlement;

    public StartSettlementEncounterAttempted(
        MobileParty party,
        Settlement settlement)
    {
        Party = party;
        Settlement = settlement;
    }
}