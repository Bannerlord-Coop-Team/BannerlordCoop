using GameInterface.Services.Heroes.Enum;
using System;
using TaleWorlds.Library;

namespace GameInterface.Services.Time.UI;

public sealed class MissionMapTimeVM : ViewModel
{
    private string mapTimeText;

    [DataSourceProperty]
    public string MapTimeText
    {
        get =>  mapTimeText;
        private set
        {
            if (mapTimeText == value) return;
            mapTimeText = value;
            OnPropertyChanged(nameof(MapTimeText));
        }
    }

    public MissionMapTimeVM(TimeControlEnum initialMode)
    {
        SetTimeControlMode(initialMode);
    }

    public void SetTimeControlMode(TimeControlEnum mode)
    {
        MapTimeText = $"Map Time: {GetModeText(mode)}";
    }
    
    internal static string GetModeText(TimeControlEnum mode)
    {
        return mode switch
        {
            TimeControlEnum.Pause => "Paused",
            TimeControlEnum.Play_1x => "Normal",
            TimeControlEnum.Play_2x => "Fast Forward",
            _ => throw new ArgumentOutOfRangeException(nameof(mode)),
        };
    }
}