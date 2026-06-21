using Common;
using GameInterface.Services.LoadingScreen.Interfaces;
using GameInterface.Services.UI.Patches;
using HarmonyLib;
using System.Reflection;
using TaleWorlds.Engine;
using TaleWorlds.MountAndBlade.GauntletUI;

namespace GameInterface.Services.UI.Interfaces;

public interface ILoadingInterface : IGameAbstraction
{
    /// <summary>
    /// True when the engine has a loading window the coop loading screen can actually be shown on,
    /// i.e. a graphical client. False on the headless server, which has no UI.
    /// </summary>
    bool IsLoadingScreenAvailable { get; }
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

    // Our own loading layer, shown alongside the engine window because the engine window does not
    // reliably paint over the main menu. Created lazily (only when actually shown, which is gated on
    // IsLoadingScreenAvailable) so it never builds any UI on the headless server.
    private static CoopTextLoadingScreen coopScreen;
    private static CoopTextLoadingScreen CoopScreen => coopScreen ??= new CoopTextLoadingScreen();

    private static readonly FieldInfo LoadingWindowViewModelField =
        AccessTools.Field(typeof(GauntletDefaultLoadingWindowManager), "_loadingWindowViewModel");

    public bool IsLoadingScreenAvailable =>
        LoadingWindow.LoadingWindowManager is GauntletDefaultLoadingWindowManager;

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

        // Bring up our own layer (which actually paints over the main menu) only for the host. The
        // client reaches ShowLoadingScreen from its network poll thread, and building a Gauntlet layer
        // off the game thread is unsafe; the client also doesn't freeze (it patches off that poll
        // thread), so it doesn't need this layer.
        if (ModInformation.IsServer && IsLoadingScreenAvailable)
        {
            CoopScreen.Show(currentTitleText, currentDescriptionText, GameModeText);
        }
    }

    public void SetLoadingMessage(string titleText, string descriptionText = "")
    {
        currentTitleText = titleText ?? string.Empty;
        currentDescriptionText = descriptionText ?? string.Empty;

        ApplyCurrentLoadingMessage();

        // Update our layer's text too, if it is up (no-op otherwise).
        coopScreen?.SetText(currentTitleText, currentDescriptionText, GameModeText);
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

        // Tear our layer down too (self-guards if it was never shown).
        coopScreen?.Hide();
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
