using GameInterface.Services.Heroes.Patches;
using TaleWorlds.CampaignSystem;

namespace GameInterface.Services.Heroes.Interaces;

internal interface ITimeControlInterface : IGameAbstraction
{
    void SetTimeControl(CampaignTimeControlMode newMode);
}

internal class TimeControlInterface : ITimeControlInterface
{
    public void SetTimeControl(CampaignTimeControlMode newMode)
    {
        TimePatches.OverrideTimeControlMode(Campaign.Current, newMode);
    }
}
