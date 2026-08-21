using Common;
using Common.Messaging;
using Common.Network;
using GameInterface.Configuration;
using GameInterface.Services.CampaignService.Messages;
using GameInterface.Services.Entity;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Players;
using GameInterface.Services.SiegeEvents.Commands;
using GameInterface.Services.Villages.Commands;
using GameInterface.Services.UI.CoopOptions;
using GameInterface.Services.UI.CoopOptions.Providers.PlayerNameplatesTab;
using GameInterface.Services.UI.CoopOptions.Providers.PlayerNameplatesTab.Sections;
using GameInterface.Services.UI.Messages;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using TaleWorlds.Library;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Engine;
using TaleWorlds.MountAndBlade;
using TaleWorlds.MountAndBlade.View.Screens;
using TaleWorlds.ScreenSystem;
using TaleWorlds.MountAndBlade.Source.Missions.Handlers;
using static TaleWorlds.Library.CommandLineFunctionality;

namespace GameInterface.Services.UI.PlayerNameplates;

/// <summary>Exposes player-nameplate state and bounded DEBUG live-test fixtures.</summary>
public static class PlayerNameplateDebugCommands
{
    [CommandLineArgumentFunction("state", "coop.debug.playermarkers")]
    public static string State(List<string> args)
    {
        if (args.Count != 0) return "Usage: coop.debug.playermarkers.state";

        var mission = Mission.Current;
        bool deploymentActive = mission?.HasMissionBehavior<DeploymentHandler>() == true;
        bool deploymentReady = mission?.GetMissionBehavior<DeploymentMissionController>()?.TeamSetupOver == true;
        ContainerProvider.TryResolve<IControllerIdProvider>(out var controllerIdProvider);
        ContainerProvider.TryResolve<IPlayerNameplateEligibility>(out var eligibility);
        var remotePlayerAgents = mission?.Agents.Where(agent =>
            agent != null && agent != Agent.Main && agent.IsActive() && agent.IsHuman &&
            agent.Character is CharacterObject character && character.HeroObject != null &&
            PlayerManager.TryGetControlledObjectInfo(character.HeroObject, out var controlInfo) &&
            controlInfo.ObjectControllerId != controllerIdProvider?.ControllerId).ToArray() ?? Array.Empty<Agent>();
        int alliedPlayerAgentCount = remotePlayerAgents.Count(agent =>
            agent.Team != null && agent.Team.IsValid && mission?.PlayerTeam != null &&
            mission.PlayerTeam.IsValid && eligibility?.IsAlliedTeam(
                agent.Team == mission.PlayerTeam,
                agent.Team.IsEnemyOf(mission.PlayerTeam)) == true);
        var view = mission?.GetMissionBehavior<PlayerNameplateMissionView>();
        if (view == null)
        {
            return "LIVE_TEST_JSON=" + JsonSerializer.Serialize(new
            {
                mission = mission?.SceneName,
                overlayAttached = false,
                visible = false,
                serverAllowed = ModConfigProvider.ModOptions.ShowPlayerNameplates,
                deploymentActive,
                deploymentReady,
                playerAgentCount = remotePlayerAgents.Length,
                alliedPlayerAgentCount,
                targetCount = 0,
                targets = Array.Empty<object>()
            });
        }

        var targets = view.Targets.Select(target => new
        {
            controllerId = target.ControllerId,
            name = target.Agent.Name?.ToString(),
            color = target.NameColor,
            teamIndex = target.Agent.Team?.TeamIndex
        }).ToArray();
        return "LIVE_TEST_JSON=" + JsonSerializer.Serialize(new
        {
            mission = mission.SceneName,
            overlayAttached = true,
            visible = view.IsVisible,
            serverAllowed = ModConfigProvider.ModOptions.ShowPlayerNameplates,
            deploymentActive,
            deploymentReady,
            playerAgentCount = remotePlayerAgents.Length,
            alliedPlayerAgentCount,
            targetCount = targets.Length,
            targets
        });
    }

    [CommandLineArgumentFunction("options_state", "coop.debug.playermarkers")]
    public static string OptionsState(List<string> args)
    {
        if (args.Count != 0) return "Usage: coop.debug.playermarkers.options_state";

        bool serverAllowed = ModConfigProvider.ModOptions.ShowPlayerNameplates;
        bool clientEnabled = false;
        if (ContainerProvider.TryResolve<ICoopOptionsStore>(out var optionsStore))
        {
            clientEnabled = PlayerNameplatesOptionsTabProvider.GetShowPlayerNameplatesOrDefault(
                optionsStore.LoadOrDefault());
        }

        return "LIVE_TEST_JSON=" + JsonSerializer.Serialize(new
        {
            serverAllowed,
            optionVisible = serverAllowed,
            clientEnabled
        });
    }

#if DEBUG
    private const int SiegeFixtureMaximumAttackerRegularTroops = 0;
    private const int SiegeFixtureMaximumDefenderRegularTroops = 1;

    private static SiegeRosterFixture siegeRosterFixture;
    private static RemotePlayerCameraBehavior remotePlayerCameraBehavior;
    private static Camera remotePlayerCamera;
    private static MatrixFrame remotePlayerCameraLocalFrame;
    private static Agent focusedRemotePlayer;
    private static string focusedRemotePlayerControllerId;
    private static string focusedRemotePlayerView = "none";

    private sealed class RemotePlayerCameraBehavior : MissionBehavior
    {
        public override MissionBehaviorType BehaviorType => MissionBehaviorType.Other;

        public override void OnPreDisplayMissionTick(float dt)
        {
            UpdateRemotePlayerCameraFrame();
        }

        public override void OnRemoveBehavior()
        {
            if (ReferenceEquals(remotePlayerCameraBehavior, this))
                remotePlayerCameraBehavior = null;
            ReleaseRemotePlayerCamera();
        }
    }

    [CommandLineArgumentFunction("focus_remote", "coop.debug.playermarkers")]
    public static string FocusRemotePlayer(List<string> args)
    {
        if (ModInformation.IsServer) return "Run this command on a client.";
        if (args.Count > 1)
            return "Usage: coop.debug.playermarkers.focus_remote [left|right|wide]";

        string view = args.Count == 1 ? args[0].ToLowerInvariant() : "wide";
        if (view != "left" && view != "right" && view != "wide")
            return "Usage: coop.debug.playermarkers.focus_remote [left|right|wide]";

        var mission = Mission.Current;
        var target = mission?.GetMissionBehavior<PlayerNameplateMissionView>()?.Targets
            .FirstOrDefault(candidate => candidate.Agent?.IsActive() == true);
        if (target?.Agent == null)
            return "No active remote player nameplate target is available.";
        if (!(ScreenManager.TopScreen is MissionScreen missionScreen) || missionScreen.CombatCamera == null)
            return "The mission screen is not active.";
        if (target.Agent.AgentVisuals?.GetEntity() == null)
            return "The remote player target has no active visual entity.";

        ReleaseRemotePlayerCamera();
        remotePlayerCamera = Camera.CreateCamera();
        remotePlayerCamera.FillParametersFrom(missionScreen.CombatCamera);
        var targetOffset = new Vec3(0f, 0f, 1.1f);
        Vec3 positionOffset;
        switch (view)
        {
            case "right":
                positionOffset = new Vec3(3.2f, -5.5f, 2.8f);
                break;
            case "wide":
                positionOffset = new Vec3(-5.5f, -9.5f, 4.4f);
                break;
            default:
                positionOffset = new Vec3(-3.2f, -5.5f, 2.8f);
                break;
        }
        remotePlayerCamera.LookAt(positionOffset, targetOffset, Vec3.Up);
        remotePlayerCameraLocalFrame = remotePlayerCamera.Frame;
        remotePlayerCamera.Entity = GameEntity.CreateEmpty(
            mission.Scene,
            isModifiableFromEditor: false,
            createPhysics: false,
            callScriptCallbacks: false);
        focusedRemotePlayer = target.Agent;
        focusedRemotePlayerControllerId = target.ControllerId;
        focusedRemotePlayerView = view;
        EnsureRemotePlayerCameraBehavior(mission);
        UpdateRemotePlayerCameraFrame();
        missionScreen.CustomCamera = remotePlayerCamera;
        return GetRemotePlayerCameraState();
    }

    [CommandLineArgumentFunction("camera_state", "coop.debug.playermarkers")]
    public static string RemotePlayerCameraState(List<string> args)
    {
        if (args.Count != 0)
            return "Usage: coop.debug.playermarkers.camera_state";

        return GetRemotePlayerCameraState();
    }

    private static string GetRemotePlayerCameraState()
    {
        if (remotePlayerCamera == null || remotePlayerCamera.Entity == null ||
            focusedRemotePlayer == null || !focusedRemotePlayer.IsActive() ||
            !(ScreenManager.TopScreen is MissionScreen missionScreen) || missionScreen.CombatCamera == null)
        {
            return "LIVE_TEST_JSON=" + JsonSerializer.Serialize(new { active = false });
        }

        UpdateRemotePlayerCameraFrame();
        MatrixFrame cameraEntityFrame = remotePlayerCamera.Entity.GetGlobalFrame();
        Vec3 renderedPosition = missionScreen.CombatCamera.Position;
        Vec3 entityDirection = -cameraEntityFrame.rotation.u;
        entityDirection.Normalize();
        Vec3 renderedDirection = missionScreen.CombatCamera.Direction;
        float directionDot =
            (renderedDirection.X * entityDirection.X) +
            (renderedDirection.Y * entityDirection.Y) +
            (renderedDirection.Z * entityDirection.Z);
        float positionDelta = (renderedPosition - cameraEntityFrame.origin).Length;
        bool active = ReferenceEquals(missionScreen.CustomCamera, remotePlayerCamera);
        return "LIVE_TEST_JSON=" + JsonSerializer.Serialize(new
        {
            active,
            controllerId = focusedRemotePlayerControllerId,
            view = focusedRemotePlayerView,
            positionDelta,
            directionDot
        });
    }

    private static void EnsureRemotePlayerCameraBehavior(Mission mission)
    {
        if (remotePlayerCameraBehavior != null && ReferenceEquals(remotePlayerCameraBehavior.Mission, mission))
            return;

        remotePlayerCameraBehavior = new RemotePlayerCameraBehavior();
        mission.AddMissionBehavior(remotePlayerCameraBehavior);
    }

    private static bool UpdateRemotePlayerCameraFrame()
    {
        if (remotePlayerCamera == null || remotePlayerCamera.Entity == null ||
            focusedRemotePlayer == null || !focusedRemotePlayer.IsActive())
        {
            return false;
        }

        GameEntity visualEntity = focusedRemotePlayer.AgentVisuals?.GetEntity();
        if (visualEntity == null) return false;

        MatrixFrame visualFrame = visualEntity.GetGlobalFrame();
        MatrixFrame globalFrame = visualFrame.TransformToParent(in remotePlayerCameraLocalFrame);
        remotePlayerCamera.Entity.SetGlobalFrame(globalFrame);
        return true;
    }

    private static void ReleaseRemotePlayerCamera()
    {
        if (remotePlayerCamera == null) return;

        if (ScreenManager.TopScreen is MissionScreen missionScreen &&
            ReferenceEquals(missionScreen.CustomCamera, remotePlayerCamera))
        {
            missionScreen.CustomCamera = null;
        }

        if (remotePlayerCamera.Entity == null)
            remotePlayerCamera.ReleaseCamera();
        else
            remotePlayerCamera.ReleaseCameraEntity();
        remotePlayerCamera = null;
        focusedRemotePlayer = null;
        focusedRemotePlayerControllerId = null;
        focusedRemotePlayerView = "none";
    }

    [CommandLineArgumentFunction("players_state", "coop.debug.playermarkers")]
    public static string PlayersState(List<string> args)
    {
        if (ModInformation.IsClient) return "Run this command on the server.";
        if (args.Count != 2)
            return "Usage: coop.debug.playermarkers.players_state <firstControllerId> <secondControllerId>";
        if (!ContainerProvider.TryResolve<IPlayerManager>(out var playerManager) ||
            !ContainerProvider.TryResolve<IObjectManager>(out var objectManager))
            return "Unable to resolve player fixture services.";

        object ResolvePlayer(string controllerId)
        {
            if (!playerManager.TryGetPlayer(controllerId, out var player) ||
                !objectManager.TryGetObject<MobileParty>(player.MobilePartyId, out var party))
                return null;

            return new
            {
                controllerId,
                mobilePartyId = player.MobilePartyId,
                partyStringId = party.StringId,
                factionId = party.MapFaction?.StringId,
                connected = playerManager.IsConnected(player),
                active = party.IsActive,
                mapEventActive = party.MapEvent != null,
                settlementId = party.CurrentSettlement?.StringId
            };
        }

        var first = ResolvePlayer(args[0]);
        var second = ResolvePlayer(args[1]);
        if (first == null || second == null)
            return "Unable to resolve both connected player parties.";

        return "LIVE_TEST_JSON=" + JsonSerializer.Serialize(new { first, second });
    }

    [CommandLineArgumentFunction("finish_shared_battle", "coop.debug.playermarkers")]
    public static string FinishSharedBattle(List<string> args)
    {
        if (ModInformation.IsClient) return "Run this command on the server.";
        if (args.Count != 2)
            return "Usage: coop.debug.playermarkers.finish_shared_battle <firstControllerId> <secondControllerId>";
        if (!ContainerProvider.TryResolve<IPlayerManager>(out var playerManager) ||
            !ContainerProvider.TryResolve<IObjectManager>(out var objectManager))
            return "Unable to resolve player fixture services.";

        MapEvent sharedMapEvent = null;
        foreach (var controllerId in args)
        {
            if (!playerManager.TryGetPlayer(controllerId, out var player) ||
                !objectManager.TryGetObject<MobileParty>(player.MobilePartyId, out var party))
                return $"Unable to resolve connected player party for {controllerId}.";
            if (party.MapEvent == null)
                return $"Player {controllerId} is not in a map event.";
            if (sharedMapEvent != null && sharedMapEvent != party.MapEvent)
                return "The players are not in the same map event.";

            sharedMapEvent = party.MapEvent;
        }

        sharedMapEvent.FinalizeEvent();
        bool success = args.All(controllerId =>
            playerManager.TryGetPlayer(controllerId, out var player) &&
            objectManager.TryGetObject<MobileParty>(player.MobilePartyId, out var party) &&
            party.MapEvent == null);
        return "LIVE_TEST_JSON=" + JsonSerializer.Serialize(new { success });
    }

    [CommandLineArgumentFunction("exit_mission", "coop.debug.playermarkers")]
    public static string ExitMission(List<string> args)
    {
        if (ModInformation.IsServer) return "Run this command on a client.";
        if (args.Count != 0) return "Usage: coop.debug.playermarkers.exit_mission";
        if (Mission.Current == null) return "No mission is active.";

        Mission.Current.EndMission();
        return "Ending the current mission.";
    }

    [CommandLineArgumentFunction("restore_player_field_battle", "coop.debug.playermarkers")]
    public static string RestorePlayerFieldBattle(List<string> args)
    {
        if (ModInformation.IsClient) return "Run this command on the server.";
        if (args.Count != 2)
            return "Usage: coop.debug.playermarkers.restore_player_field_battle <firstControllerId> <secondControllerId>";

        FinalizeSharedMapEvent(args);
        string output = MapEventDebugCommands.RestorePlayerFieldBattle(new List<string>());
        bool success = output.StartsWith("Player field-battle fixture restored.", StringComparison.Ordinal);
        return "LIVE_TEST_JSON=" + JsonSerializer.Serialize(new { success, output });
    }

    [CommandLineArgumentFunction("restore_siege", "coop.debug.playermarkers")]
    public static string RestoreSiege(List<string> args)
    {
        if (ModInformation.IsClient) return "Run this command on the server.";
        if (args.Count != 6)
            return "Usage: coop.debug.playermarkers.restore_siege <settlementId> <originalX> <originalY> <originalIsOnLand> <firstControllerId> <secondControllerId>";

        string output = null;
        bool rostersRestored;
        try
        {
            FinalizeSharedMapEvent(new List<string> { args[4], args[5] });
            output = SiegeDebugCommand.StopSiege(new List<string> { args[0], args[1], args[2], args[3] });
        }
        finally
        {
            rostersRestored = RestoreSiegeFixtureRosters(args[0]);
        }
        bool success = ContainerProvider.TryResolve<IObjectManager>(out var objectManager) &&
                       objectManager.TryGetObject<Settlement>(args[0], out var settlement) &&
                       settlement.SiegeEvent == null && rostersRestored;
        return "LIVE_TEST_JSON=" + JsonSerializer.Serialize(new { success, rostersRestored, output });
    }

    [CommandLineArgumentFunction("stage_siege_rosters", "coop.debug.playermarkers")]
    public static string StageSiegeRosters(List<string> args)
    {
        if (ModInformation.IsClient) return "Run this command on the server.";
        if (args.Count != 1) return "Usage: coop.debug.playermarkers.stage_siege_rosters <settlementId>";
        if (siegeRosterFixture != null) return $"A siege roster fixture is already active for {siegeRosterFixture.SettlementId}.";
        if (!ContainerProvider.TryResolve<IObjectManager>(out var objectManager) ||
            !objectManager.TryGetObject<Settlement>(args[0], out var settlement))
            return $"Settlement with id {args[0]} not found.";

        var camp = settlement.SiegeEvent?.BesiegerCamp;
        if (camp == null) return $"{settlement.Name} is not under siege.";

        var attackers = camp._besiegerParties
            .Where(party => party?.Party != null)
            .Select(party => party.Party)
            .ToArray();
        var defenders = settlement.GetInvolvedPartiesForEventType(MapEvent.BattleTypes.Siege)
            .Where(party => party?.MemberRoster != null)
            .ToArray();
        var parties = attackers.Concat(defenders)
            .Where(party => party?.MemberRoster != null)
            .Distinct()
            .ToArray();
        if (attackers.Length == 0 || defenders.Length == 0 || parties.Length == 0)
            return $"Unable to resolve both sides of the siege fixture at {settlement.Name}.";

        var fixture = new SiegeRosterFixture
        {
            SettlementId = settlement.StringId,
            Snapshots = parties.Select(party => new SiegeRosterSnapshot
            {
                Party = party,
                MemberRoster = party.MemberRoster.GetTroopRoster().ToArray()
            }).ToArray()
        };

        siegeRosterFixture = fixture;
        try
        {
            foreach (var party in attackers)
                MapEventDebugCommands.LimitLateJoinModeFixtureRoster(
                    party.MemberRoster,
                    SiegeFixtureMaximumAttackerRegularTroops);
            MapEventDebugCommands.LimitLateJoinModeFixtureRosters(
                defenders.Select(party => party.MemberRoster),
                SiegeFixtureMaximumDefenderRegularTroops);

            if (attackers.Any(party => party.MemberRoster.TotalHealthyCount <= 0) ||
                defenders.All(party => party.MemberRoster.TotalHealthyCount <= 0))
            {
                bool restored = RestoreSiegeFixtureRosters(settlement.StringId);
                return $"The siege roster fixture at {settlement.Name} has no healthy troops after sizing; restored={restored}.";
            }
        }
        catch (Exception exception)
        {
            bool restored = RestoreSiegeFixtureRosters(settlement.StringId);
            return $"Unable to stage the siege roster fixture: {exception.Message}; restored={restored}.";
        }

        return "LIVE_TEST_JSON=" + JsonSerializer.Serialize(new
        {
            settlementId = settlement.StringId,
            attackerPartyCount = attackers.Length,
            defenderPartyCount = defenders.Length,
            maximumAttackerRegularTroopsPerParty = SiegeFixtureMaximumAttackerRegularTroops,
            maximumDefenderRegularTroopsTotal = SiegeFixtureMaximumDefenderRegularTroops,
            attackerTroops = attackers.Sum(party => party.MemberRoster.TotalHealthyCount),
            defenderTroops = defenders.Sum(party => party.MemberRoster.TotalHealthyCount)
        });
    }

    private static bool RestoreSiegeFixtureRosters(string settlementId)
    {
        if (siegeRosterFixture == null) return true;
        if (!string.Equals(siegeRosterFixture.SettlementId, settlementId, StringComparison.Ordinal)) return false;

        var restored = RestoreSiegeFixtureRosters(siegeRosterFixture);
        if (restored) siegeRosterFixture = null;
        return restored;
    }

    private static bool RestoreSiegeFixtureRosters(SiegeRosterFixture fixture)
    {
        try
        {
            foreach (var snapshot in fixture.Snapshots)
                MapEventDebugCommands.RestoreLateJoinModeFixtureMemberRoster(snapshot.Party.MemberRoster, snapshot.MemberRoster);
            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }

    private sealed class SiegeRosterFixture
    {
        public string SettlementId { get; set; }
        public SiegeRosterSnapshot[] Snapshots { get; set; }
    }

    private sealed class SiegeRosterSnapshot
    {
        public PartyBase Party { get; set; }
        public TroopRosterElement[] MemberRoster { get; set; }
    }

    private static void FinalizeSharedMapEvent(IReadOnlyList<string> controllerIds)
    {
        if (!ContainerProvider.TryResolve<IPlayerManager>(out var playerManager) ||
            !ContainerProvider.TryResolve<IObjectManager>(out var objectManager))
            return;

        MapEvent mapEvent = null;
        foreach (var controllerId in controllerIds)
        {
            if (!playerManager.TryGetPlayer(controllerId, out var player) ||
                !objectManager.TryGetObject<MobileParty>(player.MobilePartyId, out var party) ||
                party.MapEvent == null)
                continue;

            if (mapEvent == null) mapEvent = party.MapEvent;
            else if (mapEvent != party.MapEvent) return;
        }

        if (mapEvent != null && !mapEvent.IsFinalized) mapEvent.FinalizeEvent();
    }

    [CommandLineArgumentFunction("local_visibility", "coop.debug.playermarkers")]
    public static string LocalVisibility(List<string> args)
    {
        if (ModInformation.IsServer) return "Run this command on a client.";
        if (args.Count != 1 || !bool.TryParse(args[0], out bool visible))
            return "Usage: coop.debug.playermarkers.local_visibility <true|false>";
        if (!ContainerProvider.TryResolve<ICoopOptionsStore>(out var optionsStore) ||
            !ContainerProvider.TryResolve<IMessageBroker>(out var messageBroker))
            return "Unable to resolve player-nameplate option services.";

        var options = optionsStore.LoadOrDefault();
        options.SetSection(
            PlayerNameplatesOptionsTabProvider.TabId,
            PlayerNameplatesSection.SectionId,
            new PlayerNameplatesSectionOptions { ShowPlayerNameplates = visible });
        optionsStore.Save(options);
        messageBroker.Publish(typeof(PlayerNameplateDebugCommands), new PlayerNameplateVisibilitySelected(visible));
        return $"Local player nameplates set to {visible}.";
    }

    [CommandLineArgumentFunction("server_visibility", "coop.debug.playermarkers")]
    public static string ServerVisibility(List<string> args)
    {
        if (ModInformation.IsClient) return "Run this command on the server.";
        if (args.Count != 1 || !bool.TryParse(args[0], out bool visible))
            return "Usage: coop.debug.playermarkers.server_visibility <true|false>";
        if (!ContainerProvider.TryResolve<INetwork>(out var network))
            return "Unable to resolve network.";

        var options = WithPlayerNameplates(ModConfigProvider.ModOptions, visible);
        ModConfigProvider.ModOptions = options;
        network.SendAll(new NetworkLoadModConfig(options));
        return $"Server player nameplates set to {visible} and broadcast to clients.";
    }

    private static ModOptions WithPlayerNameplates(ModOptions current, bool visible)
    {
        return new ModOptions(new ModOptionsData
        {
            FastForwardEnabled = current.FastForwardEnabled,
            AutoPauseEnabled = current.AutoPauseEnabled,
            ClientsCanUseCheats = current.ClientsCanUseCheats,
            GoldFoodInfluenceChangeInSettlements = current.GoldFoodInfluenceChangeInSettlements,
            GoldFoodInfluenceChangeInBattles = current.GoldFoodInfluenceChangeInBattles,
            GoldFoodInfluenceChangeForDisconnectedPlayers = current.GoldFoodInfluenceChangeForDisconnectedPlayers,
            PlayerBattleAiJoinWindowHours = current.PlayerBattleAiJoinWindowHours,
            SpeedLimitWhilePlayersInBattle = current.SpeedLimitWhilePlayersInBattle,
            WandererLimit = current.WandererLimit,
            WandererLimitScalesWithPlayers = current.WandererLimitScalesWithPlayers,
            PlayerKingdomClanTierRequired = current.PlayerKingdomClanTierRequired,
            SmithingStaminaRecoveryOutsideSettlements = current.SmithingStaminaRecoveryOutsideSettlements,
            SmithingStaminaRecoveryMultiplier = current.SmithingStaminaRecoveryMultiplier,
            MaximumLootersMultiplier = current.MaximumLootersMultiplier,
            LooterPartySizeMultiplier = current.LooterPartySizeMultiplier,
            LordDefectionRetries = current.LordDefectionRetries,
            EnableHeroExecutions = current.EnableHeroExecutions,
            EnablePlayerClanMemberExecutions = current.EnablePlayerClanMemberExecutions,
            ShowPlayerNameplates = visible
        });
    }
#endif
}
