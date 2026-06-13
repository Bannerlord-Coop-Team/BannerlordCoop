using Common.Messaging;
using TaleWorlds.CampaignSystem.Settlements;

namespace GameInterface.Services.Villages.Messages;

/// <summary>
/// When village tax has changed.
/// </summary>
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