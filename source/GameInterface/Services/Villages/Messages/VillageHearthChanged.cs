using Common.Messaging;
using TaleWorlds.CampaignSystem.Settlements;

namespace GameInterface.Services.Villages.Messages;

/// <summary>
/// Used when the hearth changes in a village.
/// </summary>
public readonly struct VillageHearthChanged : ICommand
{
    public readonly Village Village;
    public readonly float Hearth;

    public VillageHearthChanged(Village village, float hearth)
    {
        Village = village;
        Hearth = hearth;
    }
}