using Common.Logging.Attributes;
using Common.Messaging;

namespace GameInterface.Services.Villages.Messages;

/// <summary>
/// When Village Tax has changed
/// </summary>
[BatchLogMessage]
public record VillageTaxAccumulateChanged : ICommand
{
    public string VilageId { get; }

    public int TradeTaxAccumulated { get; }

    public VillageTaxAccumulateChanged(string vilageId, int tradeTaxAccumulated)
    {
        VilageId = vilageId;
        TradeTaxAccumulated = tradeTaxAccumulated;
    }
}
