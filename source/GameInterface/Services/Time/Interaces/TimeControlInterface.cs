using GameInterface.Services.Heroes.Enum;
using GameInterface.Services.Heroes.Patches;
using GameInterface.Services.Time;
using TaleWorlds.CampaignSystem;

namespace GameInterface.Services.Heroes.Interaces;

internal interface ITimeControlInterface : IGameAbstraction
{
    TimeControlEnum GetTimeControl();
    void SetTimeControl(TimeControlEnum newMode);
}

internal class TimeControlInterface : ITimeControlInterface
{
    private readonly ITimeControlModeConverter modeConverter;

    public TimeControlInterface(ITimeControlModeConverter modeConverter)
    {
        this.modeConverter = modeConverter;
    }

    public TimeControlEnum GetTimeControl()
    {
        return modeConverter.Convert(Campaign.Current.TimeControlMode);
    }

    public void SetTimeControl(TimeControlEnum newMode)
    {
        TimePatches.OverrideTimeControlMode(Campaign.Current, modeConverter.Convert(newMode));
    }
}
