using Common;
using Common.Messaging;
using GameInterface.Configuration;
using GameInterface.Services.CampaignService.Messages;
using GameInterface.Services.Entity;
using GameInterface.Services.Locations;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Players;
using GameInterface.Services.UI.CoopOptions;
using GameInterface.Services.UI.CoopOptions.Providers.PlayerNameplatesTab;
using GameInterface.Services.UI.Messages;
using System;
using System.Collections.Generic;
using System.Linq;
using SandBox.Tournaments.MissionLogics;
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
    private readonly IPlayerNameplateControllerResolver controllerResolver;
    private readonly IPlayerNameplateEligibility eligibility;
    private readonly IPlayerManager playerManager;
    private readonly IObjectManager objectManager;

    private PlayerNameplatesVM dataSource;
    private GauntletLayer gauntletLayer;
    private GauntletMovieIdentifier movie;
    private bool serverAllowsPlayerNameplates;
    private bool showPlayerNameplates;
    private float targetRefreshElapsed;

    public bool IsVisible => dataSource?.IsEnabled == true;
    public IReadOnlyList<PlayerNameplateTargetVM> Targets =>
        dataSource?.Targets?.ToArray() ?? Array.Empty<PlayerNameplateTargetVM>();

    public PlayerNameplateMissionView(
        IMessageBroker messageBroker,
        ICoopOptionsStore optionsStore,
        IPlayerKillFeedColorService colorService,
        IPlayerNameplateControllerResolver controllerResolver,
        IPlayerNameplateEligibility eligibility,
        IPlayerManager playerManager,
        IObjectManager objectManager)
    {
        this.messageBroker = messageBroker;
        this.optionsStore = optionsStore;
        this.colorService = colorService;
        this.controllerResolver = controllerResolver;
        this.eligibility = eligibility;
        this.playerManager = playerManager;
        this.objectManager = objectManager;
    }

    public override void OnMissionScreenInitialize()
    {
        base.OnMissionScreenInitialize();

        dataSource = new PlayerNameplatesVM { IsEnabled = false };
        gauntletLayer = new GauntletLayer("PlayerNameplates", LayerOrder);
        movie = gauntletLayer.LoadMovie("PlayerNameplates", dataSource);
        MissionScreen.AddLayer(gauntletLayer);

        messageBroker.Subscribe<ModConfigApplied>(HandleModConfigApplied);
        messageBroker.Subscribe<PlayerNameplateVisibilitySelected>(HandleVisibilitySelected);
        serverAllowsPlayerNameplates = ModConfigProvider.ModOptions.ShowPlayerNameplates;
        ApplyVisibility(PlayerNameplatesOptionsTabProvider.GetShowPlayerNameplatesOrDefault(
            optionsStore.LoadOrDefault()));
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
            {
                target.SetNameColor(colorService.GetColorString(controllerId));
                if (TryGetPlayerHeroName(controllerId, out var playerHeroName))
                    target.SetPlayerHeroName(playerHeroName);
            }
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
        {
            messageBroker.Unsubscribe<PlayerNameplateVisibilitySelected>(HandleVisibilitySelected);
            messageBroker.Unsubscribe<ModConfigApplied>(HandleModConfigApplied);
        }

        if (gauntletLayer != null)
        {
            if (movie != null) gauntletLayer.ReleaseMovie(movie);
            MissionScreen.RemoveLayer(gauntletLayer);
        }

        dataSource?.OnFinalize();
        controllerResolver.Dispose();
        movie = null;
        gauntletLayer = null;
        dataSource = null;

        base.OnMissionScreenFinalize();
    }

    private void RefreshTargets()
    {
        if (dataSource == null || Mission == null) return;

        var eligibleAgents = new Dictionary<Agent, PlayerNameplateTargetData>();
        foreach (var agent in Mission.Agents)
        {
            if (!TryGetControllerId(agent, out var controllerId) ||
                !TryGetPlayerHeroName(controllerId, out var playerHeroName))
                continue;

            eligibleAgents[agent] = new PlayerNameplateTargetData(controllerId, playerHeroName);
        }

        for (int i = dataSource.Targets.Count - 1; i >= 0; i--)
        {
            var target = dataSource.Targets[i];
            if (eligibleAgents.TryGetValue(target.Agent, out var targetData) &&
                target.ControllerId == targetData.ControllerId)
            {
                target.SetPlayerHeroName(targetData.PlayerHeroName);
                eligibleAgents.Remove(target.Agent);
                continue;
            }

            dataSource.Targets.RemoveAt(i);
            target.OnFinalize();
        }

        foreach (var eligibleAgent in eligibleAgents)
        {
            dataSource.Targets.Add(new PlayerNameplateTargetVM(
                eligibleAgent.Key,
                eligibleAgent.Value.ControllerId,
                eligibleAgent.Value.PlayerHeroName,
                colorService.GetColorString(eligibleAgent.Value.ControllerId)));
        }
    }

    private void HandleVisibilitySelected(MessagePayload<PlayerNameplateVisibilitySelected> payload)
    {
        GameThread.RunSafe(() =>
        {
            ApplyVisibility(payload.What.ShowPlayerNameplates);
        }, context: nameof(PlayerNameplateMissionView));
    }

    private void HandleModConfigApplied(MessagePayload<ModConfigApplied> payload)
    {
        serverAllowsPlayerNameplates = payload.What.ModOptions.ShowPlayerNameplates;
        ApplyVisibility(PlayerNameplatesOptionsTabProvider.GetShowPlayerNameplatesOrDefault(
            optionsStore.LoadOrDefault()));
    }

    private void ApplyVisibility(bool clientAllowsPlayerNameplates)
    {
        if (dataSource == null) return;

        showPlayerNameplates = serverAllowsPlayerNameplates && clientAllowsPlayerNameplates;
        dataSource.IsEnabled = showPlayerNameplates;
        if (showPlayerNameplates) RefreshTargets();
    }

    private bool TryGetControllerId(Agent agent, out string controllerId)
    {
        controllerId = null;

        if (agent == null || agent == Agent.Main || !agent.IsActive() || !agent.IsHuman) return false;
        var playerTeam = Mission?.PlayerTeam;
        if (agent.Team == null || playerTeam == null) return false;

        bool bothTeamsInvalid = !agent.Team.IsValid && !playerTeam.IsValid;
        bool hasMixedTeamValidity = agent.Team.IsValid != playerTeam.IsValid;
        bool isEnemyOfPlayerTeam = agent.Team.IsValid && playerTeam.IsValid && agent.Team.IsEnemyOf(playerTeam);
        bool isTournament = Mission.GetMissionBehavior<TournamentBehavior>() != null;
        if (!eligibility.IsAlliedTeam(
            agent.Team == playerTeam,
            isEnemyOfPlayerTeam,
            bothTeamsInvalid,
            hasMixedTeamValidity,
            isTournament))
            return false;
        if (!controllerResolver.TryGetControllerId(agent, out controllerId)) return false;

        return !string.IsNullOrEmpty(controllerId);
    }

    private bool TryGetPlayerHeroName(string controllerId, out string playerHeroName)
    {
        playerHeroName = null;
        if (!playerManager.TryGetPlayer(controllerId, out var player) ||
            !objectManager.TryGetObject(player.HeroId, out Hero playerHero))
            return false;

        playerHeroName = playerHero.Name?.ToString();
        return !string.IsNullOrEmpty(playerHeroName);
    }

    private readonly struct PlayerNameplateTargetData
    {
        public readonly string ControllerId;
        public readonly string PlayerHeroName;

        public PlayerNameplateTargetData(string controllerId, string playerHeroName)
        {
            ControllerId = controllerId;
            PlayerHeroName = playerHeroName;
        }
    }
}
