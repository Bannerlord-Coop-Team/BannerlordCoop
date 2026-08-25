using TaleWorlds.Engine;

namespace GameInterface.Services.UI.Interfaces;

// Raw seam over the engine's global loading window toggles. Deliberately does NOT touch
// LoadingWindowPatches.ForceLoadingWindow the way ILoadingInterface does, so a Disable here
// is not suppressed and the battle mission's own load-completion disable (MissionPatches)
// still clears the window once the loaded battle is running.
public interface IGlobalLoadingWindow : IGameAbstraction
{
    void Enable();
    void Disable();
}

internal sealed class GlobalLoadingWindow : IGlobalLoadingWindow
{
    public void Enable() => LoadingWindow.EnableGlobalLoadingWindow();
    public void Disable() => LoadingWindow.DisableGlobalLoadingWindow();
}
