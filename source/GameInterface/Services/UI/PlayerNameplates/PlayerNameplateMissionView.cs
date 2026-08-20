using Common;
using Common.Messaging;
using GameInterface.Configuration;
using GameInterface.Services.Entity;
using GameInterface.Services.Locations;
using GameInterface.Services.Players;
using GameInterface.Services.UI.CoopOptions;
using GameInterface.Services.UI.CoopOptions.Providers.PlayerNameplatesTab;
using GameInterface.Services.UI.Messages;
using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Engine.GauntletUI;
using TaleWorlds.MountAndBlade;
using TaleWorlds.MountAndBlade.View.MissionViews;

namespace GameInterface.Services.UI.PlayerNameplates;

/// <summary>Displays colored names above allied remote player agents.</summary>
public sealed class PlayerNameplateMissionView : MissionView, ILocationMissionBehavior
{
    private const int LayerOrder = 2;
    private const float TargetRefreshInterval = 0.5f;

    private readonly IMessageBroker messageBroker;
    private readonly ICoopOptionsStore optionsStore;
    private readonly IPlayerKillFeedColorService colorService;
    private readonly IControllerIdProvider controllerIdProvider;
    private readonly IPlayerNameplateEligibility eligibility;

    private PlayerNameplatesVM dataSource;
    private GauntletLayer gauntletLayer;
    private GauntletMovieIdentifier movie;
    private bool showPlayerNameplates;
    private float targetRefreshElapsed;

    public bool IsVisible => dataSource?.IsEnabled == true;
    public IReadOnlyList<PlayerNameplateTargetVM> Targets =>
        dataSource?.Targets?.ToArray() ?? Array.Empty<PlayerNameplateTargetVM>();

    public PlayerNameplateMissionView(
        IMessageBroker messageBroker,
        ICoopOptionsStore optionsStore,
        IPlayerKillFeedColorService colorService,
        IControllerIdProvider controllerIdProvider,
        IPlayerNameplateEligibility eligibility)
    {
        this.messageBroker = messageBroker;
        this.optionsStore = optionsStore;
        this.colorService = colorService;
        this.controllerIdProvider = controllerIdProvider;
        this.eligibility = eligibility;
    }

    public override void OnMissionScreenInitialize()
    {
        base.OnMissionScreenInitialize();

        if (!ModConfigProvider.ModOptions.ShowPlayerNameplates) return;

        showPlayerNameplates = PlayerNameplatesOptionsTabProvider.GetShowPlayerNameplatesOrDefault(
            optionsStore.LoadOrDefault());
        dataSource = new PlayerNameplatesVM { IsEnabled = showPlayerNameplates };
        gauntletLayer = new GauntletLayer("PlayerNameplates", LayerOrder);
        movie = gauntletLayer.LoadMovie("PlayerNameplates", dataSource);
        MissionScreen.AddLayer(gauntletLayer);

        messageBroker.Subscribe<PlayerNameplateVisibilitySelected>(HandleVisibilitySelected);
        RefreshTargets();
    }

    public override void OnMissionScreenTick(float dt)
    {
        base.OnMissionScreenTick(dt);

        if (dataSource == null || !showPlayerNameplates) return;

        targetRefreshElapsed += dt;
        if (targetRefreshElapsed >= TargetRefreshInterval)
        {
            targetRefreshElapsed = 0f;
            RefreshTargets();
        }

        var camera = MissionScreen.CombatCamera;
        if (camera == null) return;

        foreach (var target in dataSource.Targets)
        {
            target.UpdatePosition(camera);
            if (TryGetControllerId(target.Agent, out var controllerId))
                target.SetNameColor(colorService.GetColorString(controllerId));
        }
    }

    public override void OnPhotoModeActivated()
    {
        if (gauntletLayer != null) gauntletLayer.UIContext.ContextAlpha = 0f;
    }

    public override void OnPhotoModeDeactivated()
    {
        if (gauntletLayer != null) gauntletLayer.UIContext.ContextAlpha = 1f;
    }

    public override void OnMissionScreenFinalize()
    {
        if (dataSource != null)
            messageBroker.Unsubscribe<PlayerNameplateVisibilitySelected>(HandleVisibilitySelected);

        if (gauntletLayer != null)
        {
            if (movie != null) gauntletLayer.ReleaseMovie(movie);
            MissionScreen.RemoveLayer(gauntletLayer);
        }

        dataSource?.OnFinalize();
        movie = null;
        gauntletLayer = null;
        dataSource = null;

        base.OnMissionScreenFinalize();
    }

    private void RefreshTargets()
    {
        if (dataSource == null || Mission == null) return;

        var eligibleAgents = new HashSet<Agent>();
        foreach (var agent in Mission.Agents)
        {
            if (TryGetControllerId(agent, out _)) eligibleAgents.Add(agent);
        }

        for (int i = dataSource.Targets.Count - 1; i >= 0; i--)
        {
            var target = dataSource.Targets[i];
            if (eligibleAgents.Remove(target.Agent)) continue;

            dataSource.Targets.RemoveAt(i);
            target.OnFinalize();
        }

        foreach (var agent in eligibleAgents)
        {
            if (!TryGetControllerId(agent, out var controllerId)) continue;

            dataSource.Targets.Add(new PlayerNameplateTargetVM(
                agent,
                controllerId,
                colorService.GetColorString(controllerId)));
        }
    }

    private void HandleVisibilitySelected(MessagePayload<PlayerNameplateVisibilitySelected> payload)
    {
        GameThread.RunSafe(() =>
        {
            if (dataSource == null) return;

            showPlayerNameplates = payload.What.ShowPlayerNameplates &&
                                   ModConfigProvider.ModOptions.ShowPlayerNameplates;
            dataSource.IsEnabled = showPlayerNameplates;
            if (showPlayerNameplates) RefreshTargets();
        }, context: nameof(PlayerNameplateMissionView));
    }

    private bool TryGetControllerId(Agent agent, out string controllerId)
    {
        controllerId = null;

        if (agent == null || agent == Agent.Main || !agent.IsActive() || !agent.IsHuman) return false;
        var playerTeam = Mission?.PlayerTeam;
        if (agent.Team == null || !agent.Team.IsValid || playerTeam == null || !playerTeam.IsValid)
            return false;
        if (!eligibility.IsAlliedTeam(agent.Team == playerTeam, agent.Team.IsEnemyOf(playerTeam)))
            return false;
        if (agent.Character is not CharacterObject character || character.HeroObject == null) return false;
        if (!PlayerManager.TryGetControlledObjectInfo(character.HeroObject, out var controlInfo)) return false;
        if (controlInfo.ObjectControllerId == controllerIdProvider.ControllerId) return false;

        controllerId = controlInfo.ObjectControllerId;
        return !string.IsNullOrEmpty(controllerId);
    }

}
