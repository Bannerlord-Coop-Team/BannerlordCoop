using GameInterface.Services.UI.Patches;
using TaleWorlds.Engine;

namespace GameInterface.Services.UI.Interfaces;

public interface ILoadingInterface : IGameAbstraction
{
    void HideLoadingScreen();
    void ShowLoadingScreen();
}

internal class LoadingInterface : ILoadingInterface
{
    public void ShowLoadingScreen()
    {
        LoadingWindowPatches.ForceLoadingWindow = true;

        // Mark the window active even if no manager exists yet (e.g. mid state-transition),
        // so the engine re-enables it the next time the manager is (re)initialized.
        LoadingWindow.IsLoadingWindowActive = true;

        // Show it immediately if a manager is already present.
        LoadingWindow.EnableGlobalLoadingWindow();
    }

    public void HideLoadingScreen()
    {
        LoadingWindowPatches.ForceLoadingWindow = false;

        LoadingWindow.DisableGlobalLoadingWindow();
    }
}
