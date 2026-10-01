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

    public void ClearTargets()
    {
        foreach (var target in Targets)
        {
            target.OnFinalize();
        }

        Targets.Clear();
    }

    public override void OnFinalize()
    {
        ClearTargets();
        base.OnFinalize();
    }
}

/// <summary>Projects one remote player's colored name into screen space.</summary>
public sealed class PlayerNameplateTargetVM : MissionAgentMarkerTargetVM
{
    private string nameColor;
    private bool isSpeaking;

    // Native name markers rewrite child visibility and alpha, so hide the sprite through its color and width.
    [DataSourceProperty] public string SpeakingIconColor => isSpeaking ? "#7FC875FF" : "#7FC87500";
    [DataSourceProperty] public float SpeakingIconWidth => isSpeaking ? 32f : 0f;

    // Shows or collapses the speaker icon without changing the player's name or marker visibility.
    public void SetSpeaking(bool value)
    {
        if (isSpeaking == value) return;
        isSpeaking = value;
        OnPropertyChanged(nameof(SpeakingIconColor));
        OnPropertyChanged(nameof(SpeakingIconWidth));
    }


    public PlayerNameplateTargetVM(
        Agent agent,
        string controllerId,
        string playerHeroName,
        string nameColor) : base(agent)
    {
        Agent = agent;
        ControllerId = controllerId;
        Name = playerHeroName;
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

    public void SetPlayerHeroName(string value)
    {
        if (Name == value) return;

        Name = value;
    }
}
