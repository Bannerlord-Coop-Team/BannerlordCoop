using Common.Logging.Attributes;
using Common.Messaging;
using TaleWorlds.CampaignSystem.Settlements;

namespace GameInterface.Services.Villages.Messages;

/// <summary>
/// When village tax has changed.
/// </summary>
[BatchLogMessage]
public readonly struct VillageTaxAccumulateChanged : ICommand
{
    public readonly Village Village;
    public readonly int TradeTaxAccumulated;

    public VillageTaxAccumulateChanged(Village village, int tradeTaxAccumulated)
    {
        Village = village;
        TradeTaxAccumulated = tradeTaxAccumulated;
    }
}