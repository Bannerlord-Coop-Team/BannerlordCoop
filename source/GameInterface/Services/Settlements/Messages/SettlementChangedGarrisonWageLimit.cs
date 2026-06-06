using Common.Logging.Attributes;
using Common.Messaging;
using TaleWorlds.CampaignSystem.Settlements;

namespace GameInterface.Services.Settlements.Messages;

/// <summary>
/// Settlement client changed garrison wage limit
/// </summary>
[BatchLogMessage]
public readonly struct SettlementChangedGarrisonWageLimit : IEvent
{
    public readonly Settlement Settlement;
    public readonly int GarrisonWagePaymentLimit;

    public SettlementChangedGarrisonWageLimit(Settlement settlement, int garrisonWagePaymentLimit)
    {
        Settlement = settlement;
        GarrisonWagePaymentLimit = garrisonWagePaymentLimit;
    }
}