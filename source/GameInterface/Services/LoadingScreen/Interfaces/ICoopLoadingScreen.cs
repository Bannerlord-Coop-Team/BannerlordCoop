using System;
using TaleWorlds.Core;
using TaleWorlds.Engine;
using TaleWorlds.Engine.GauntletUI;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
using TaleWorlds.ScreenSystem;
using TaleWorlds.TwoDimension;

namespace GameInterface.Services.LoadingScreen.Interfaces;
/// <summary>
/// Class to create and destroy loading screen
/// </summary>
internal class CoopLoadingScreen : GlobalLayer
{
    private static CoopLoadingScreen _instance;

    private GauntletLayer _gauntletLayer;

    private CoopLoadingWindowVM _loadingWindowViewModel;

    private SpriteCategory _sploadingCategory;

    private SpriteCategory _mpLoadingCategory;

    private SpriteCategory _mpBackgroundCategory;

    private bool _isMultiplayer;

    private bool _isInitialized = false;

    private bool _isActive = false;

    public void EnableLoadingWindow()
    {
        if (!_isActive)
        {
            this._loadingWindowViewModel.Enabled = true;
            ScreenManager.AddGlobalLayer(this, false);
            _isActive = true;
        }
    }

    public static CoopLoadingScreen Instance
    {
        get
        {
            if(_instance == null)
            {
                _instance = new CoopLoadingScreen();
            }
            return _instance;
        }
    }

    public void DisableLoadingWindow()
    {
        if (_isActive)
        {
            this._loadingWindowViewModel.Enabled = false;
            ScreenManager.RemoveGlobalLayer(this);
            _isActive = false;
        }
    }

    public void Initialize()
    {
        if (!_isInitialized)
        {
            this._sploadingCategory = UIResourceManager.SpriteData.SpriteCategories["ui_loading"];
            this._sploadingCategory.InitializePartialLoad();
            this._loadingWindowViewModel = new CoopLoadingWindowVM(new Action<bool, int>(this.HandleSPPartialLoading));
            this._loadingWindowViewModel.Enabled = false;
            this._loadingWindowViewModel.SetTotalGenericImageCount(this._sploadingCategory.SpriteSheetCount);
            bool shouldClear = true;
            this._gauntletLayer = new GauntletLayer(100003, "GauntletLayer", shouldClear);
            this._gauntletLayer.LoadMovie("LoadingWindow", this._loadingWindowViewModel);
            base.Layer = this._gauntletLayer;
            _isInitialized = true;
        }
    }

    public void SetCurrentModeIsMultiplayer(bool isMultiplayer)
    {
        if (this._isMultiplayer != isMultiplayer)
        {
            this._isMultiplayer = isMultiplayer;
            this._loadingWindowViewModel.IsMultiplayer = isMultiplayer;
            if (isMultiplayer)
            {
                this._mpLoadingCategory = UIResourceManager.SpriteData.SpriteCategories["ui_mploading"];
                this._mpLoadingCategory.Load(UIResourceManager.ResourceContext, UIResourceManager.UIResourceDepot);
                this._mpBackgroundCategory = UIResourceManager.SpriteData.SpriteCategories["ui_mpbackgrounds"];
                this._mpBackgroundCategory.Load(UIResourceManager.ResourceContext, UIResourceManager.UIResourceDepot);
                return;
            }
            this._mpLoadingCategory.Unload();
            this._mpBackgroundCategory.Unload();
        }
    }

    private void HandleSPPartialLoading(bool isLoading, int index)
    {
        if (isLoading)
        {
            SpriteCategory sploadingCategory = this._sploadingCategory;
            if (sploadingCategory == null)
            {
                return;
            }
            sploadingCategory.PartialLoadAtIndex(UIResourceManager.ResourceContext, UIResourceManager.UIResourceDepot, index);
            return;
        }
        else
        {
            SpriteCategory sploadingCategory2 = this._sploadingCategory;
            if (sploadingCategory2 == null)
            {
                return;
            }
            sploadingCategory2.PartialUnloadAtIndex(index);
            return;
        }
    }
}

public class CoopLoadingWindowVM : ViewModel
{
    private int _currentImage;

    private int _totalGenericImageCount;

    public Action<bool, int> _handleSPPartialLoading;

    private bool _enabled;

    private bool _isDevelopmentMode;

    private bool _isMultiplayer;

    private string _loadingImageName;

    private string _titleText;

    private string _descriptionText;

    private string _gameModeText;

    public bool CurrentlyShowingMultiplayer { get; private set; }

    [DataSourceProperty]
    public bool Enabled
    {
        get
        {
            return _enabled;
        }
        set
        {
            if (_enabled != value)
            {
                _enabled = value;
                OnPropertyChangedWithValue(value, "Enabled");
                if (value)
                {
                    HandleEnable();
                }
            }
        }
    }

    [DataSourceProperty]
    public bool IsDevelopmentMode
    {
        get
        {
            return _isDevelopmentMode;
        }
        set
        {
            if (_isDevelopmentMode != value)
            {
                _isDevelopmentMode = value;
                OnPropertyChangedWithValue(value, "IsDevelopmentMode");
            }
        }
    }

    [DataSourceProperty]
    public string TitleText
    {
        get
        {
            return _titleText;
        }
        set
        {
            if (_titleText != value)
            {
                _titleText = value;
                OnPropertyChangedWithValue(value, "TitleText");
            }
        }
    }

    [DataSourceProperty]
    public string GameModeText
    {
        get
        {
            return _gameModeText;
        }
        set
        {
            if (_gameModeText != value)
            {
                _gameModeText = value;
                OnPropertyChangedWithValue(value, "GameModeText");
            }
        }
    }

    [DataSourceProperty]
    public string DescriptionText
    {
        get
        {
            return _descriptionText;
        }
        set
        {
            if (_descriptionText != value)
            {
                _descriptionText = value;
                OnPropertyChangedWithValue(value, "DescriptionText");
            }
        }
    }

    [DataSourceProperty]
    public bool IsMultiplayer
    {
        get
        {
            return _isMultiplayer;
        }
        set
        {
            if (_isMultiplayer != value)
            {
                _isMultiplayer = value;
                OnPropertyChangedWithValue(value, "IsMultiplayer");
            }
        }
    }

    [DataSourceProperty]
    public string LoadingImageName
    {
        get
        {
            return _loadingImageName;
        }
        set
        {
            if (_loadingImageName != value)
            {
                _loadingImageName = value;
                OnPropertyChangedWithValue(value, "LoadingImageName");
            }
        }
    }

    public CoopLoadingWindowVM(Action<bool, int> handleSPPartialLoading)
    {
        _handleSPPartialLoading = handleSPPartialLoading;
        _handleSPPartialLoading?.Invoke(arg1: true, _currentImage + 1);
    }

    internal void Update()
    {
        if (Enabled)
        {
            bool flag = IsEligableForMultiplayerLoading();
            if (flag && !CurrentlyShowingMultiplayer)
            {
                SetForMultiplayer();
            }
            else if (!flag && CurrentlyShowingMultiplayer)
            {
                SetForEmpty();
            }
        }
    }

    private void HandleEnable()
    {
        if (IsEligableForMultiplayerLoading())
        {
            SetForMultiplayer();
        }
        else
        {
            SetForEmpty();
        }
    }

    private bool IsEligableForMultiplayerLoading()
    {
        if (_isMultiplayer && TaleWorlds.MountAndBlade.Mission.Current != null)
        {
            return Game.Current.GameStateManager.ActiveState is MissionState;
        }

        return false;
    }

    private void SetForMultiplayer()
    {
        MissionState missionState = (MissionState)Game.Current.GameStateManager.ActiveState;
        string text = missionState.MissionName switch
        {
            "MultiplayerTeamDeathmatch" => "TeamDeathmatch",
            "MultiplayerSiege" => "Siege",
            "MultiplayerBattle" => "Battle",
            "MultiplayerCaptain" => "Captain",
            "MultiplayerSkirmish" => "Skirmish",
            "MultiplayerDuel" => "Duel",
            _ => missionState.MissionName,
        };
        if (!string.IsNullOrEmpty(text))
        {
            DescriptionText = GameTexts.FindText("str_multiplayer_official_game_type_explainer", text).ToString();
        }
        else
        {
            DescriptionText = "";
        }

        GameModeText = GameTexts.FindText("str_multiplayer_official_game_type_name", text).ToString();
        if (GameTexts.TryGetText("str_multiplayer_scene_name", out var textObject, missionState.CurrentMission.SceneName))
        {
            TitleText = textObject.ToString();
        }
        else
        {
            TitleText = missionState.CurrentMission.SceneName;
        }

        LoadingImageName = missionState.CurrentMission.SceneName;
        CurrentlyShowingMultiplayer = true;
    }

    private void SetForEmpty()
    {
        DescriptionText = "";
        TitleText = "";
        GameModeText = "";
        SetNextGenericImage();
        CurrentlyShowingMultiplayer = false;
    }

    private void SetNextGenericImage()
    {
        if (_currentImage != 3)
        {
            _handleSPPartialLoading?.Invoke(arg1: false, _currentImage);
        }

        // Load image 03, default loading screen image
        _currentImage = 3;
        _handleSPPartialLoading?.Invoke(arg1: true, _currentImage);

        IsDevelopmentMode = NativeConfig.IsDevelopmentMode;
        LoadingImageName = "loading_" + _currentImage.ToString("00");
    }

    public void SetTotalGenericImageCount(int totalGenericImageCount)
    {
        _totalGenericImageCount = totalGenericImageCount;
    }
}

