using TaleWorlds.Library;

namespace GameInterface.Services.Voice;

/// <summary>Positions a speaking icon above one campaign party without accepting input.</summary>
public sealed class VoicePartyMarkerVM : ViewModel
{
    public string ControllerId { get; }
    [DataSourceProperty] public float X { get; private set; }
    [DataSourceProperty] public float Y { get; private set; }
    [DataSourceProperty] public bool IsVisible { get; private set; }

    // Keeps the marker attached to a stable player identity.
    public VoicePartyMarkerVM(string controllerId) => ControllerId = controllerId;

    // Centers the icon over the projected party head and hides parties outside the map view.
    public void UpdatePosition(float x, float y, bool visible)
    {
        X = x - 16f;
        Y = y - 32f;
        IsVisible = visible;
        OnPropertyChanged(nameof(X));
        OnPropertyChanged(nameof(Y));
        OnPropertyChanged(nameof(IsVisible));
    }
}
