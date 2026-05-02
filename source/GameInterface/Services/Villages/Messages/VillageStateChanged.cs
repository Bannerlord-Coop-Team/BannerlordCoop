using Common.Messaging;
using TaleWorlds.CampaignSystem.Settlements;

namespace GameInterface.Services.Villages.Messages;

/// <summary>
/// This event is fired when the village state is updated.
/// </summary>
public readonly struct VillageStateChanged : ICommand
{
    public readonly Village Village;
    public readonly int State;

    public VillageStateChanged(Village village, int state)
    {
        Village = village;
        State = state;
    }
}