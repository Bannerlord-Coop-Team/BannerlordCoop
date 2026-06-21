using TaleWorlds.Engine.GauntletUI;
using TaleWorlds.Library;
using TaleWorlds.ScreenSystem;

namespace GameInterface.Services.LoadingScreen.Interfaces;

/// <summary>
/// A coop-owned loading screen layer that renders over whatever screen is active, including the
/// main menu. The engine's own loading window does not reliably paint over the main menu (the
/// engine resets its enabled state while the menu is the top screen), so the host shows this
/// instead while it applies its patches off the main thread. It is a thin global layer over the
/// stock "LoadingWindow" movie with a view model that only carries the displayed text, with none
/// of the multiplayer/image logic that would otherwise overwrite it.
/// </summary>
internal sealed class CoopTextLoadingScreen : GlobalLayer
{
    // Sorts above the main-menu layer (order 1) and any screen the host start runs over.
    private const int LayerOrder = 100003;

    private GauntletLayer gauntletLayer;
    private GauntletMovieIdentifier movie;
    private CoopTextLoadingWindowVM viewModel;

    public void Show(string titleText, string descriptionText, string gameModeText)
    {
        // Build a fresh layer + movie each time it is shown (and tear it down in Hide) rather than
        // reusing one across host sessions, so a re-host always gets a clean, correctly rendered screen.
        if (gauntletLayer == null)
        {
            viewModel = new CoopTextLoadingWindowVM
            {
                Enabled = false,
                // The generic single-player loading backdrop. If its sprite is not loaded the widget
                // just shows the black background, which is still a loading screen, not a frozen menu.
                LoadingImageName = "loading_03",
            };

            // shouldClear paints the backdrop so the menu underneath is not visible through it.
            gauntletLayer = new GauntletLayer("GauntletLayer", LayerOrder, shouldClear: true);
            movie = gauntletLayer.LoadMovie("LoadingWindow", viewModel);
            Layer = gauntletLayer;

            ScreenManager.AddGlobalLayer(this, false);
        }

        viewModel.TitleText = titleText ?? string.Empty;
        viewModel.DescriptionText = descriptionText ?? string.Empty;
        viewModel.GameModeText = gameModeText ?? string.Empty;
        viewModel.Enabled = true;
    }

    public void SetText(string titleText, string descriptionText, string gameModeText)
    {
        if (viewModel == null) return;

        viewModel.TitleText = titleText ?? string.Empty;
        viewModel.DescriptionText = descriptionText ?? string.Empty;
        viewModel.GameModeText = gameModeText ?? string.Empty;
    }

    public void Hide()
    {
        if (gauntletLayer == null) return;

        viewModel.Enabled = false;
        ScreenManager.RemoveGlobalLayer(this);
        gauntletLayer.ReleaseMovie(movie);
        Layer = null;
        gauntletLayer = null;
        viewModel = null;
    }
}

/// <summary>
/// View model for <see cref="CoopTextLoadingScreen"/>. Holds only what the stock "LoadingWindow"
/// movie binds. Deliberately has none of the multiplayer/image-cycling logic of the engine's own
/// loading view model, which clears the text when enabled outside a multiplayer mission.
/// </summary>
public sealed class CoopTextLoadingWindowVM : ViewModel
{
    private bool enabled;
    private string titleText = string.Empty;
    private string descriptionText = string.Empty;
    private string gameModeText = string.Empty;
    private string loadingImageName = string.Empty;
    private bool isMultiplayer;
    private bool isDevelopmentMode;

    [DataSourceProperty]
    public bool Enabled
    {
        get => enabled;
        set { if (enabled != value) { enabled = value; OnPropertyChangedWithValue(value, nameof(Enabled)); } }
    }

    [DataSourceProperty]
    public string TitleText
    {
        get => titleText;
        set { if (titleText != value) { titleText = value; OnPropertyChangedWithValue(value, nameof(TitleText)); } }
    }

    [DataSourceProperty]
    public string DescriptionText
    {
        get => descriptionText;
        set { if (descriptionText != value) { descriptionText = value; OnPropertyChangedWithValue(value, nameof(DescriptionText)); } }
    }

    [DataSourceProperty]
    public string GameModeText
    {
        get => gameModeText;
        set { if (gameModeText != value) { gameModeText = value; OnPropertyChangedWithValue(value, nameof(GameModeText)); } }
    }

    [DataSourceProperty]
    public string LoadingImageName
    {
        get => loadingImageName;
        set { if (loadingImageName != value) { loadingImageName = value; OnPropertyChangedWithValue(value, nameof(LoadingImageName)); } }
    }

    [DataSourceProperty]
    public bool IsMultiplayer
    {
        get => isMultiplayer;
        set { if (isMultiplayer != value) { isMultiplayer = value; OnPropertyChangedWithValue(value, nameof(IsMultiplayer)); } }
    }

    [DataSourceProperty]
    public bool IsDevelopmentMode
    {
        get => isDevelopmentMode;
        set { if (isDevelopmentMode != value) { isDevelopmentMode = value; OnPropertyChangedWithValue(value, nameof(IsDevelopmentMode)); } }
    }
}
