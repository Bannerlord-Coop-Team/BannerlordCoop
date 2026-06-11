using Common.Logging.Attributes;
using Common.Messaging;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;

namespace GameInterface.Services.Settlements.Messages;

/// <summary>
/// Notify server to send message about mobile party cache change.
/// </summary>
[BatchLogMessage]
public readonly struct SettlementChangedMobileParty : IEvent
{
    public readonly Settlement Settlement;
    public readonly MobileParty MobileParty;
    public readonly bool AddMobileParty;

    public SettlementChangedMobileParty(
        Settlement settlement,
        MobileParty mobileParty,
        bool addMobileParty)
    {
        Settlement = settlement;
        MobileParty = mobileParty;
        AddMobileParty = addMobileParty;
    }
}