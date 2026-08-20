using SandBox.ViewModelCollection.Missions.NameMarker.Targets;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace GameInterface.Services.UI.PlayerNameplates;

/// <summary>Provides the active player-nameplate targets to Gauntlet.</summary>
public sealed class PlayerNameplatesVM : ViewModel
{
    private bool isEnabled;

    public PlayerNameplatesVM()
    {
        Targets = new MBBindingList<PlayerNameplateTargetVM>();
    }

    [DataSourceProperty]
    public MBBindingList<PlayerNameplateTargetVM> Targets { get; }

    [DataSourceProperty]
    public bool IsEnabled
    {
        get => isEnabled;
        set
        {
            if (isEnabled == value) return;

            isEnabled = value;
            OnPropertyChanged(nameof(IsEnabled));
        }
    }

    public override void OnFinalize()
    {
        foreach (var target in Targets)
        {
            target.OnFinalize();
        }

        Targets.Clear();
        base.OnFinalize();
    }
}

/// <summary>Projects one remote player's colored name into screen space.</summary>
public sealed class PlayerNameplateTargetVM : MissionAgentMarkerTargetVM
{
    private string nameColor;

    public PlayerNameplateTargetVM(Agent agent, string controllerId, string nameColor) : base(agent)
    {
        Agent = agent;
        ControllerId = controllerId;
        this.nameColor = nameColor;
        IconType = string.Empty;
        IsEnabled = true;
    }

    public Agent Agent { get; }
    public string ControllerId { get; }

    [DataSourceProperty]
    public string NameColor
    {
        get => nameColor;
        private set
        {
            if (nameColor == value) return;

            nameColor = value;
            OnPropertyChanged(nameof(NameColor));
        }
    }

    public void SetNameColor(string value)
    {
        NameColor = value;
    }
}
