using GameInterface.Services.UI.Patches;
using HarmonyLib;
using System.Reflection;
using TaleWorlds.Engine;
using TaleWorlds.MountAndBlade.GauntletUI;

namespace GameInterface.Services.UI.Interfaces;

public interface ILoadingInterface : IGameAbstraction
{
    void HideLoadingScreen();
    void ShowLoadingScreen();
    void ShowLoadingScreen(string titleText, string descriptionText = "");
    void SetLoadingMessage(string titleText, string descriptionText = "");
}

internal class LoadingInterface : ILoadingInterface
{
    private const string GameModeText = "Coop";
    private static string currentTitleText = string.Empty;
    private static string currentDescriptionText = string.Empty;

    private static readonly FieldInfo LoadingWindowViewModelField =
        AccessTools.Field(typeof(GauntletDefaultLoadingWindowManager), "_loadingWindowViewModel");

    public void ShowLoadingScreen()
    {
        LoadingWindowPatches.ForceLoadingWindow = true;

        // Mark the window active even if no manager exists yet (e.g. mid state-transition),
        // so the engine re-enables it the next time the manager is (re)initialized.
        LoadingWindow.IsLoadingWindowActive = true;

        // Show it immediately if a manager is already present.
        LoadingWindow.EnableGlobalLoadingWindow();
    }

    public void ShowLoadingScreen(string titleText, string descriptionText = "")
    {
        ShowLoadingScreen();
        SetLoadingMessage(titleText, descriptionText);
    }

    public void SetLoadingMessage(string titleText, string descriptionText = "")
    {
        currentTitleText = titleText ?? string.Empty;
        currentDescriptionText = descriptionText ?? string.Empty;

        ApplyCurrentLoadingMessage();
    }

    internal static void ApplyCurrentLoadingMessage()
    {
        if (string.IsNullOrEmpty(currentTitleText) &&
            string.IsNullOrEmpty(currentDescriptionText))
        {
            return;
        }

        var viewModel = GetLoadingWindowViewModel();
        if (viewModel == null) return;

        viewModel.GameModeText = GameModeText;
        viewModel.TitleText = currentTitleText;
        viewModel.DescriptionText = currentDescriptionText;
    }

    public void HideLoadingScreen()
    {
        LoadingWindowPatches.ForceLoadingWindow = false;
        currentTitleText = string.Empty;
        currentDescriptionText = string.Empty;

        LoadingWindow.DisableGlobalLoadingWindow();
    }

    private static LoadingWindowViewModel GetLoadingWindowViewModel()
    {
        if (LoadingWindow.LoadingWindowManager is not GauntletDefaultLoadingWindowManager manager)
        {
            return null;
        }

        return LoadingWindowViewModelField?.GetValue(manager) as LoadingWindowViewModel;
    }
}
