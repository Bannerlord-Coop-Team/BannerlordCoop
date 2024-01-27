using Common.Logging.Attributes;
using Common.Messaging;

namespace GameInterface.Services.Villages.Messages;

/// <summary>
/// When TradeTaxAccumulated needs to be changed on one of the villages.
/// </summary>
[DontLogMessage]
public record ChangeVillageTradeTaxAccumulated : ICommand
{
    public string VillageId { get; }

    public int TradeTaxAccumulated { get; }

    public ChangeVillageTradeTaxAccumulated(string villageId, int tradeTaxAccumulated)
    {
        VillageId = villageId;
        TradeTaxAccumulated = tradeTaxAccumulated;
    }
}
