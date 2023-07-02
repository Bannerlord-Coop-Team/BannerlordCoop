using GameInterface.Services.Heroes.Patches;
using TaleWorlds.CampaignSystem;

namespace GameInterface.Services.Heroes.Interaces;

internal interface ITimeControlInterface : IGameAbstraction
{
    void PauseAndDisableTimeControls();
    void EnableTimeControls();
    void SetTimeControl(CampaignTimeControlMode newMode);
}

internal class TimeControlInterface : ITimeControlInterface
{
    internal static bool IsTimeLocked = true;

    public void PauseAndDisableTimeControls()
    {
        if (Campaign.Current == null) return;

        Campaign.Current.SetTimeControlModeLock(false);
        Campaign.Current.TimeControlMode = CampaignTimeControlMode.Stop;
        IsTimeLocked = true;
    }

    public void EnableTimeControls()
    {
        IsTimeLocked = false;
    }

    public void SetTimeControl(CampaignTimeControlMode newMode)
    {
        if (IsTimeLocked) return;

        TimePatches.OverrideTimeControlMode(Campaign.Current, newMode);
    }
}
