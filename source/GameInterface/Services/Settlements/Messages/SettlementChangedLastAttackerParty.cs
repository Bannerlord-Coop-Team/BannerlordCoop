using Common.Messaging;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.Settlements.Messages;

/// <summary>
/// Changed Settlement.LastAttackerParty.
/// </summary>
public readonly struct SettlementChangedLastAttackerParty : IEvent
{
    public readonly Settlement Settlement;
    public readonly MobileParty AttackerParty;

    public SettlementChangedLastAttackerParty(Settlement settlement, MobileParty attackerParty)
    {
        Settlement = settlement;
        AttackerParty = attackerParty;
    }
}