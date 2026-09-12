using Common.Messaging;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;

namespace GameInterface.Services.SiegeEvents.Messages;

/// <summary>
/// A siege assault map event started on the server; defending players need their encounter prompt.
/// </summary>
public readonly struct SiegeAssaultStarted : IEvent
{
    public readonly MobileParty AttackerParty;
    public readonly Settlement Settlement;

    public SiegeAssaultStarted(MobileParty attackerParty, Settlement settlement)
    {
        AttackerParty = attackerParty;
        Settlement = settlement;
    }
}
