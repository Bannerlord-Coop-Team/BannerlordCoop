using Common.Messaging;
using TaleWorlds.CampaignSystem.Settlements;

namespace GameInterface.Services.Villages.Messages;

/// <summary>
/// Message used when LastDemandTimeSatisfied changes.
/// </summary>
public readonly struct VillageDemandTimeChanged : ICommand
{
    public readonly Village Village;
    public readonly float LastDemandSatisfiedTime;

    public VillageDemandTimeChanged(Village village, float lastDemandSatisfiedTime)
    {
        Village = village;
        LastDemandSatisfiedTime = lastDemandSatisfiedTime;
    }
}