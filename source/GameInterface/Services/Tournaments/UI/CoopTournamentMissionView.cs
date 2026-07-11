using GameInterface.Services.Tournaments.Data;
using SandBox.GauntletUI.Missions;
using SandBox.Tournaments.MissionLogics;
using SandBox.View.Missions.Tournaments;
using TaleWorlds.CampaignSystem.TournamentGames;
using TaleWorlds.Core;
using TaleWorlds.Engine;
using TaleWorlds.Engine.GauntletUI;
using TaleWorlds.InputSystem;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
using TaleWorlds.MountAndBlade.View;
using TaleWorlds.MountAndBlade.View.MissionViews;
using TaleWorlds.ScreenSystem;

namespace GameInterface.Services.Tournaments.UI;

[OverrideView(typeof(MissionTournamentView))]
public sealed class CoopTournamentMissionView : MissionGauntletTournamentView
{
    private TournamentSessionSnapshot initialSnapshot;
    private TournamentBehavior behavior;
    private Camera customCamera;
    private bool viewEnabled;
    private GauntletMovieIdentifier movie;
    private GauntletLayer gauntletLayer;
    private CoopTournamentVM dataSource;
    private bool viewModeResolved;
    private bool isCoopView;

    public CoopTournamentMissionView()
    {
    }

    public CoopTournamentMissionView(TournamentSessionSnapshot initialSnapshot)
    {
        this.initialSnapshot = initialSnapshot;
    }

    public override void AfterStart()
    {
        if (!IsCoopView())
        {
            base.AfterStart();
            return;
        }

        behavior = Mission.GetMissionBehavior<TournamentBehavior>();
        var cameraEntity = Mission.Scene.FindEntityWithTag("camera_instance");
        if (cameraEntity != null)
        {
            customCamera = Camera.CreateCamera();
            var cameraPosition = new Vec3();
            cameraEntity.GetCameraParamsFromCameraScript(customCamera, ref cameraPosition);
        }
    }

    public override void OnMissionScreenInitialize()
    {
        if (!IsCoopView())
        {
            base.OnMissionScreenInitialize();
            return;
        }

        if (behavior == null ||
            !ContainerProvider.TryResolve<TournamentUIController>(out var controller) ||
            !TryResolveInitialSnapshot(out initialSnapshot))
            return;

        dataSource = new CoopTournamentVM(DisableUi, behavior, initialSnapshot, controller);
        gauntletLayer = new GauntletLayer("MissionCoopTournament", ViewOrderPriority);
        gauntletLayer.Input.RegisterHotKeyCategory(HotKeyManager.GetCategory("GenericPanelGameKeyCategory"));
        gauntletLayer.InputRestrictions.SetInputRestrictions();
        gauntletLayer.IsFocusLayer = true;
        ScreenManager.TrySetFocus(gauntletLayer);

        var gameKeyCategory = HotKeyManager.GetCategory("GenericPanelGameKeyCategory");
        dataSource.SetDoneInputKey(gameKeyCategory.GetHotKey("Confirm"));
        dataSource.SetCancelInputKey(gameKeyCategory.GetHotKey("Exit"));
        movie = gauntletLayer.LoadMovie("CoopTournamentScreen", dataSource);

        MissionScreen.AddLayer(gauntletLayer);
        if (dataSource.ShouldShowUI)
            ShowUi();
        else
            DisableUi();
    }

    public override void OnMissionScreenFinalize()
    {
        if (!IsCoopView())
        {
            base.OnMissionScreenFinalize();
            return;
        }

        if (gauntletLayer != null)
        {
            gauntletLayer.IsFocusLayer = false;
            ScreenManager.TryLoseFocus(gauntletLayer);
            gauntletLayer.InputRestrictions.ResetInputRestrictions();
        }

        dataSource?.OnFinalize();
        if (gauntletLayer != null)
        {
            if (movie != null)
                gauntletLayer.ReleaseMovie(movie);
            MissionScreen.RemoveLayer(gauntletLayer);
        }

        if (initialSnapshot != null &&
            ContainerProvider.TryResolve<TournamentMissionUIContext>(out var missionUIContext))
            missionUIContext.Clear(initialSnapshot.SessionId);

        movie = null;
        gauntletLayer = null;
        dataSource = null;
        customCamera = null;
        initialSnapshot = null;
    }

    public override void OnMissionTick(float dt)
    {
        if (!IsCoopView())
        {
            base.OnMissionTick(dt);
            return;
        }

        if (behavior == null || dataSource == null || gauntletLayer == null) return;

        dataSource.RefreshPendingBracket();
        UpdateBetInput();

        if (dataSource.ShouldShowUI && !viewEnabled)
        {
            dataSource.Refresh();
            ShowUi();
        }
        else if (!dataSource.ShouldShowUI && viewEnabled)
        {
            DisableUi();
        }
        UpdateCombatUi();
    }

    public override bool IsOpeningEscapeMenuOnFocusChangeAllowed()
        => IsCoopView() ? !viewEnabled : base.IsOpeningEscapeMenuOnFocusChangeAllowed();

    public override void OnAgentRemoved(
        Agent affectedAgent,
        Agent affectorAgent,
        AgentState agentState,
        KillingBlow killingBlow)
    {
        if (!IsCoopView())
        {
            base.OnAgentRemoved(affectedAgent, affectorAgent, agentState, killingBlow);
            return;
        }

        dataSource?.OnCoopAgentRemoved(affectedAgent);
    }

    public override void OnPhotoModeActivated()
    {
        if (!IsCoopView())
        {
            base.OnPhotoModeActivated();
            return;
        }

        if (gauntletLayer != null) gauntletLayer.UIContext.ContextAlpha = 0f;
    }

    public override void OnPhotoModeDeactivated()
    {
        if (!IsCoopView())
        {
            base.OnPhotoModeDeactivated();
            return;
        }

        if (gauntletLayer != null) gauntletLayer.UIContext.ContextAlpha = viewEnabled ? 1f : 0f;
        UpdateCombatUi();
    }

    private void UpdateBetInput()
    {
        if (!dataSource.IsBetWindowEnabled) return;

        if (gauntletLayer.Input.IsHotKeyReleased("Confirm"))
        {
            UISoundsHelper.PlayUISound("event:/ui/default");
            dataSource.ExecuteBet();
            dataSource.IsBetWindowEnabled = false;
        }
        else if (gauntletLayer.Input.IsHotKeyReleased("Exit"))
        {
            UISoundsHelper.PlayUISound("event:/ui/default");
            dataSource.IsBetWindowEnabled = false;
        }
    }

    private void DisableUi()
    {
        if (viewEnabled && customCamera != null)
            MissionScreen.UpdateFreeCamera(customCamera.Frame);
        MissionScreen.CustomCamera = null;
        viewEnabled = false;
        if (gauntletLayer == null) return;

        UpdateCombatUi();
        gauntletLayer.InputRestrictions.ResetInputRestrictions();
        if (gauntletLayer.IsFocusLayer)
        {
            gauntletLayer.IsFocusLayer = false;
            ScreenManager.TryLoseFocus(gauntletLayer);
        }
    }

    private void ShowUi()
    {
        if (viewEnabled) return;

        if (customCamera != null)
            MissionScreen.CustomCamera = customCamera;
        viewEnabled = true;
        if (gauntletLayer == null) return;

        gauntletLayer.UIContext.ContextAlpha = 1f;

        gauntletLayer.InputRestrictions.SetInputRestrictions();
        if (!gauntletLayer.IsFocusLayer)
        {
            gauntletLayer.IsFocusLayer = true;
            ScreenManager.TrySetFocus(gauntletLayer);
        }
    }

    private void UpdateCombatUi()
    {
        if (gauntletLayer == null) return;

        gauntletLayer.UIContext.ContextAlpha =
            viewEnabled || dataSource?.IsCurrentMatchActive == true ? 1f : 0f;
    }

    private bool TryResolveInitialSnapshot(out TournamentSessionSnapshot snapshot)
    {
        if (initialSnapshot != null)
        {
            snapshot = initialSnapshot;
            return true;
        }

        if (ContainerProvider.TryResolve<TournamentMissionUIContext>(out var missionUIContext))
            return missionUIContext.TryGet(out snapshot);

        snapshot = null;
        return false;
    }

    private bool IsCoopView()
    {
        if (viewModeResolved)
            return isCoopView;

        isCoopView = TryResolveInitialSnapshot(out initialSnapshot);
        viewModeResolved = true;
        return isCoopView;
    }
}
