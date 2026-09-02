using Common.Commands;
using Common.Logging;
using GameInterface.Configuration;
using GameInterface.Services.MobileParties.Extensions;
using GameInterface.Utils.Commands;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;
using TaleWorlds.Engine.GauntletUI;
using TaleWorlds.GauntletUI.BaseTypes;
using TaleWorlds.InputSystem;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
using TaleWorlds.MountAndBlade.GauntletUI.Mission.Singleplayer;
using TaleWorlds.MountAndBlade.GauntletUI.Widgets.Scoreboard;

namespace GameInterface.Services.MapEvents.Commands;

/// <summary>
/// Battle fixture commands for deployment, scoreboard inspection, mission exit, and combat outcomes. Run the
/// direct kill commands on the battle-authority client because it owns the AI/enemy
/// agents, so each kill goes through the coop death path: <c>Agent.Die</c>, the mission death callback,
/// the death broadcast, and the server-roster casualty, exactly like <c>coop.debug.mapevent.kms</c>.
/// </summary>
internal class BattleTeamKillCommands
{
    private static CoopCommandResult Succeeded(string output) =>
        new CoopCommandResult(true, output);

    private static CoopCommandResult Failed(string output) =>
        new CoopCommandResult(false, output, "command_failed");

    public static readonly ILogger Logger = LogManager.GetLogger<BattleTeamKillCommands>();

    private const string ScoreboardMovieName = "SPScoreboard";
    private const string PartyScoreToggleWidgetId = "PartyScoreToggleWidget";
    private const string PartyDetailsWidgetId = "PartyDetails";

    public sealed class ClickDeploymentReadyCoopCommand : ICoopCommand
    {
        public string Prefix => "coop.debug.map_event";

        public string Name => "click_deployment_ready";

        public string Description => "Runs the click deployment ready debug operation.";

        public IExpectedArgs[] ExpectedArgs { get; } = Array.Empty<IExpectedArgs>();

        public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
        {
            var mission = Mission.Current;
            if (mission is null)
                return Failed("Failed: no active mission.");

            var deploymentController = mission.GetMissionBehavior<DeploymentMissionController>();
            if (deploymentController == null)
                return Failed("No active deployment.");
            if (!deploymentController.TeamSetupOver)
                return Failed("Failed: deployment team setup is not complete.");

            var orderUi = mission.GetMissionBehavior<MissionGauntletSingleplayerOrderUIHandler>();
            if (orderUi == null)
                return Failed("Failed: no deployment order UI.");

            orderUi.OnBeginMission();
            return Succeeded("Clicked deployment Ready through the native UI callback.");
        }
    }

    public sealed class DeploymentStateCoopCommand : ICoopCommand
    {
        public string Prefix => "coop.debug.map_event";

        public string Name => "deployment_state";

        public string Description => "Reports deployment state.";

        public IExpectedArgs[] ExpectedArgs { get; } = Array.Empty<IExpectedArgs>();

        public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
        {
            var mission = Mission.Current;
            if (mission == null)
                return Succeeded("Deployment state: mission=False, controller=False, teamSetupOver=False, handler=False.");

            var controller = mission.GetMissionBehavior<DeploymentMissionController>();
            var handler = mission.GetMissionBehavior<DeploymentHandler>();
            return Succeeded($"Deployment state: mission=True, controller={controller != null}, " +
                   $"teamSetupOver={controller?.TeamSetupOver ?? false}, handler={handler != null}.");
        }
    }

    public sealed class FinishDeploymentCoopCommand : ICoopCommand
    {
        public string Prefix => "coop.debug.map_event";

        public string Name => "finish_deployment";

        public string Description => "Runs the finish deployment debug operation.";

        public IExpectedArgs[] ExpectedArgs { get; } = Array.Empty<IExpectedArgs>();

        public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
        {
            var mission = Mission.Current;
            if (mission is null)
                return Failed("Failed: no active mission.");

            var deploymentController = mission.GetMissionBehavior<DeploymentMissionController>();
            if (deploymentController == null)
                return Failed("No active deployment.");
            if (!deploymentController.TeamSetupOver)
                return Failed("Failed: deployment team setup is not complete.");

            var deploymentHandler = mission.GetMissionBehavior<DeploymentHandler>();
            if (deploymentHandler == null)
                return Failed("Failed: no deployment handler.");

            deploymentHandler.FinishDeployment();
            return Succeeded("Finished the current deployment.");
        }
    }

    public sealed class ToggleScoreboardCoopCommand : ICoopCommand
    {
        public string Prefix => "coop.debug.map_event";

        public string Name => "toggle_scoreboard";

        public string Description => "Runs the toggle scoreboard debug operation.";

        public IExpectedArgs[] ExpectedArgs { get; } = Array.Empty<IExpectedArgs>();

        public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
        {
            var mission = Mission.Current;
            if (mission is null)
                return Failed("Failed: no active mission.");

            var scoreboard = mission.GetMissionBehavior<MissionGauntletBattleScore>();
            if (scoreboard?.DataSource == null)
                return Failed("Failed: no battle scoreboard UI.");

            if (mission.InputManager is ScoreboardInputContext scoreboardInput)
            {
                mission.InputManager = scoreboardInput.Inner;
                return Succeeded("Released the native scoreboard input.");
            }
            if (mission.InputManager == null)
                return Failed("Failed: no mission input context.");

            mission.InputManager = new ScoreboardInputContext(mission.InputManager);
            return Succeeded("Holding the native scoreboard input.");
        }
    }

    public sealed class CollapseScoreboardPartiesCoopCommand : ICoopCommand
    {
        public string Prefix => "coop.debug.map_event";

        public string Name => "collapse_scoreboard_parties";

        public string Description => "Runs the collapse scoreboard parties debug operation.";

        public IExpectedArgs[] ExpectedArgs { get; } = Array.Empty<IExpectedArgs>();

        public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
        {
            var scoreboard = Mission.Current?.GetMissionBehavior<MissionGauntletBattleScore>();
            var dataSource = scoreboard?.DataSource;
            if (dataSource == null)
                return Failed("Failed: no battle scoreboard UI.");

            if (!TryGetScoreboardWidgets(scoreboard, out var scrollablePanel, out var partyHeaderCount, out var partyDetails))
                return Failed("Failed: native scoreboard widgets are not loaded.");

            var expectedPartyCount = dataSource.Attackers.Parties.Count + dataSource.Defenders.Parties.Count;
            if (partyHeaderCount != expectedPartyCount || partyDetails.Count != expectedPartyCount)
                return Failed($"Failed: found {partyHeaderCount} native party headers and {partyDetails.Count} party detail panels, " +
                       $"expected {expectedPartyCount} each.");

            var verticalScrollbar = scrollablePanel.VerticalScrollbar;

            foreach (var partyDetail in partyDetails)
                partyDetail.IsVisible = false;

            scrollablePanel.ResetTweenSpeed();
            verticalScrollbar.ValueFloat = verticalScrollbar.MinValue;
            scrollablePanel.SetVerticalScrollTarget(verticalScrollbar.MinValue, 0f);
            return Succeeded($"Collapsed native party details: {partyHeaderCount}/{expectedPartyCount}.");
        }
    }

    public sealed class ScoreboardStateCoopCommand : ICoopCommand
    {
        public string Prefix => "coop.debug.map_event";

        public string Name => "scoreboard_state";

        public string Description => "Reports scoreboard state.";

        public IExpectedArgs[] ExpectedArgs { get; } = Array.Empty<IExpectedArgs>();

        public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
        {
            var mission = Mission.Current;
            if (mission is null)
                return Failed("Failed: no active mission.");

            var scoreboard = mission.GetMissionBehavior<MissionGauntletBattleScore>();
            var dataSource = scoreboard?.DataSource;
            if (dataSource == null)
                return Failed("Failed: no battle scoreboard UI.");
            if (dataSource.Attackers?.Parties == null || dataSource.Defenders?.Parties == null)
                return Failed("Failed: battle scoreboard parties are not loaded.");

            var mapEvent = MobileParty.MainParty?.MapEvent;
            if (mapEvent == null)
                return Failed("Failed: the main party has no current map event.");

            var expectedParties = mapEvent.InvolvedParties
                .Where(party => party != null)
                .Distinct()
                .ToArray();
            var expectedPlayerParties = expectedParties
                .Where(party => party.MobileParty?.IsPlayerParty() == true)
                .ToArray();
            if (expectedPlayerParties.Length == 0)
                return Failed("Failed: the current map event has no registered player parties.");
            var scoreboardParties = dataSource.Attackers.Parties
                .Concat(dataSource.Defenders.Parties)
                .Select(party => party.BattleCombatant)
                .OfType<PartyBase>()
                .Distinct()
                .ToArray();
            var scoreboardPlayerParties = scoreboardParties
                .Where(party => party.MobileParty?.IsPlayerParty() == true)
                .ToArray();
            var missingParties = expectedParties.Except(scoreboardParties).ToArray();
            var missingPlayerParties = expectedPlayerParties.Except(scoreboardParties).ToArray();
            var expandedPartyDetails = 0;
            var scrollTop = false;
            var nativeWidgetsLoaded = TryGetScoreboardWidgets(
                scoreboard,
                out var scrollablePanel,
                out var partyHeaderCount,
                out var partyDetails);
            if (nativeWidgetsLoaded)
            {
                expandedPartyDetails = partyDetails.Count(details => details.IsVisible);
                var scrollbar = scrollablePanel.VerticalScrollbar;
                scrollTop = Math.Abs(scrollbar.ValueFloat - scrollbar.MinValue) < 0.01f;
            }

            return Succeeded($"Visible: {dataSource.ShowScoreboard}; " +
                   $"Expected parties ({expectedParties.Length}): {FormatPartyNames(expectedParties)}; " +
                   $"Expected player parties ({expectedPlayerParties.Length}): {FormatPartyNames(expectedPlayerParties)}; " +
                   $"Scoreboard parties ({scoreboardParties.Length}): {FormatPartyNames(scoreboardParties)}; " +
                   $"Scoreboard player parties ({scoreboardPlayerParties.Length}): {FormatPartyNames(scoreboardPlayerParties)}; " +
                   $"Missing parties ({missingParties.Length}): {FormatPartyNames(missingParties)}; " +
                   $"Missing player parties ({missingPlayerParties.Length}): {FormatPartyNames(missingPlayerParties)}; " +
                   $"Native widgets loaded: {nativeWidgetsLoaded}; " +
                   $"Party headers ({partyHeaderCount}); Expanded party details ({expandedPartyDetails}); Scroll top: {scrollTop}");
        }
    }

    private static bool TryGetScoreboardWidgets(
        MissionGauntletBattleScore scoreboard,
        out ScrollablePanel scrollablePanel,
        out int partyHeaderCount,
        out List<Widget> partyDetails)
    {
        scrollablePanel = null;
        partyHeaderCount = 0;
        partyDetails = new List<Widget>();

        var rootWidget = scoreboard.MissionScreen?.Layers
            .OfType<GauntletLayer>()
            .Select(layer => layer.GetMovieIdentifier(ScoreboardMovieName))
            .FirstOrDefault(identifier => identifier?.Movie?.RootWidget != null)?
            .Movie.RootWidget;
        if (rootWidget == null)
            return false;

        var widgets = rootWidget.GetAllChildrenRecursive();
        scrollablePanel = widgets.OfType<ScrollablePanel>()
            .FirstOrDefault(panel => panel.VerticalScrollbar != null);
        partyHeaderCount = widgets.Count(widget => widget.Id == PartyScoreToggleWidgetId);
        partyDetails = widgets.Where(widget => widget.Id == PartyDetailsWidgetId).ToList();
        return scrollablePanel != null;
    }

    private static string FormatPartyNames(IEnumerable<PartyBase> parties)
    {
        var names = parties.Select(party => party.Name?.ToString() ?? "<unnamed>").ToArray();
        return names.Length == 0 ? "<none>" : string.Join(", ", names);
    }

    private sealed class ScoreboardInputContext : IInputContext
    {
        public IInputContext Inner { get; }

        public ScoreboardInputContext(IInputContext inner)
        {
            Inner = inner;
        }

        public int GetPointerX() => Inner.GetPointerX();
        public int GetPointerY() => Inner.GetPointerY();
        public System.Numerics.Vector2 GetPointerPosition() => Inner.GetPointerPosition();
        public bool IsGameKeyDown(int gameKey) => Inner.IsGameKeyDown(gameKey);
        public bool IsGameKeyDownImmediate(int gameKey) => Inner.IsGameKeyDownImmediate(gameKey);
        public bool IsGameKeyPressed(int gameKey) => Inner.IsGameKeyPressed(gameKey);
        public bool IsGameKeyReleased(int gameKey) => Inner.IsGameKeyReleased(gameKey);
        public float GetGameKeyAxis(string gameAxisKey) => Inner.GetGameKeyAxis(gameAxisKey);
        public bool IsHotKeyDown(string hotKey) =>
            hotKey == ScoreboardHotKeyCategory.HoldShow || Inner.IsHotKeyDown(hotKey);
        public bool IsHotKeyReleased(string hotKey) => Inner.IsHotKeyReleased(hotKey);
        public bool IsHotKeyPressed(string hotKey) => Inner.IsHotKeyPressed(hotKey);
        public bool IsHotKeyDoublePressed(string hotKey) => Inner.IsHotKeyDoublePressed(hotKey);
        public Vec2 GetKeyState(InputKey key) => Inner.GetKeyState(key);
        public bool IsKeyDown(InputKey key) => Inner.IsKeyDown(key);
        public bool IsKeyPressed(InputKey key) => Inner.IsKeyPressed(key);
        public bool IsKeyReleased(InputKey key) => Inner.IsKeyReleased(key);
        public float GetMouseMoveX() => Inner.GetMouseMoveX();
        public float GetMouseMoveY() => Inner.GetMouseMoveY();
        public bool GetIsMouseActive() => Inner.GetIsMouseActive();
        public Vec2 GetMousePositionPixel() => Inner.GetMousePositionPixel();
        public float GetDeltaMouseScroll() => Inner.GetDeltaMouseScroll();
        public bool GetIsControllerConnected() => Inner.GetIsControllerConnected();
        public Vec2 GetMousePositionRanged() => Inner.GetMousePositionRanged();
        public float GetMouseSensitivity() => Inner.GetMouseSensitivity();
        public bool IsControlDown() => Inner.IsControlDown();
        public bool IsShiftDown() => Inner.IsShiftDown();
        public bool IsAltDown() => Inner.IsAltDown();
        public Vec2 GetControllerRightStickState() => Inner.GetControllerRightStickState();
        public Vec2 GetControllerLeftStickState() => Inner.GetControllerLeftStickState();
        public InputKey[] GetClickKeys() => Inner.GetClickKeys();
    }

    public sealed class LeaveBattleCoopCommand : ICoopCommand
    {
        public string Prefix => "coop.debug.map_event";

        public string Name => "leave_battle";

        public string Description => "Runs the leave battle debug operation.";

        public IExpectedArgs[] ExpectedArgs { get; } = Array.Empty<IExpectedArgs>();

        public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
        {
            if (!ModConfigProvider.ModOptions.ClientsCanUseCheats)
                return Failed("The host has disabled cheats on clients.");

            var mission = Mission.Current;
            if (mission is null)
                return Failed("Failed: no active mission.");

            mission.EndMission();
            return Succeeded("Left the current battle mission.");
        }
    }

    public sealed class KillEnemyCoopCommand : ICoopCommand
    {
        public string Prefix => "coop.debug.map_event";

        public string Name => "kill_enemy";

        public string Description => "Runs the kill enemy debug operation.";

        public IExpectedArgs[] ExpectedArgs { get; } = Array.Empty<IExpectedArgs>();

        public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
        {
            if (!ModConfigProvider.ModOptions.ClientsCanUseCheats)
                return Failed("The host has disabled cheats on clients.");

            if (!TryGetEnemyAgents(out var agents, out var failure))
                return Failed(failure);

            var agent = agents.FirstOrDefault();
            if (agent is null)
                return Failed("No live enemy agents to kill.");

            try
            {
                Kill(agent);
            }
            catch (Exception ex)
            {
                return Failed(CommandHelpers.FormatException("Kill enemy", ex));
            }

            return Succeeded($"Killed enemy agent: {agent.Name}");
        }
    }

    public sealed class KillEnemyTeamCoopCommand : ICoopCommand
    {
        public string Prefix => "coop.debug.map_event";

        public string Name => "kill_enemy_team";

        public string Description => "Runs the kill enemy team debug operation.";

        public IExpectedArgs[] ExpectedArgs { get; } = Array.Empty<IExpectedArgs>();

        public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
        {
            if (!ModConfigProvider.ModOptions.ClientsCanUseCheats)
                return Failed("The host has disabled cheats on clients.");

            if (!TryGetEnemyAgents(out var agents, out var failure))
                return Failed(failure);

            var killed = KillAll(agents, out var ex);
            if (ex != null)
                return Failed(CommandHelpers.FormatException("Kill enemy team", ex));

            return Succeeded($"Killed {killed} enemy agent(s).");
        }
    }

    public sealed class KillOwnTeamCoopCommand : ICoopCommand
    {
        public string Prefix => "coop.debug.map_event";

        public string Name => "kill_own_team";

        public string Description => "Runs the kill own team debug operation.";

        public IExpectedArgs[] ExpectedArgs { get; } = Array.Empty<IExpectedArgs>();

        public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
        {
            if (!ModConfigProvider.ModOptions.ClientsCanUseCheats)
                return Failed("The host has disabled cheats on clients.");

            var mission = Mission.Current;
            if (mission is null)
                return Failed("Failed: no active mission.");
            if (mission.PlayerTeam is null)
                return Failed("Failed: no player team in this mission.");

            var agents = mission.PlayerTeam.ActiveAgents.ToList();
            var killed = KillAll(agents, out var ex);
            if (ex != null)
                return Failed(CommandHelpers.FormatException("Kill own team", ex));

            return Succeeded($"Killed {killed} agent(s) on the local player team.");
        }
    }

    /// <summary>Live agents on any team hostile to the player (host) team.</summary>
    private static bool TryGetEnemyAgents(out List<Agent> agents, out string failure)
    {
        agents = null;
        failure = null;

        var mission = Mission.Current;
        if (mission is null) { failure = "Failed: no active mission."; return false; }
        if (mission.PlayerTeam is null) { failure = "Failed: no player team in this mission."; return false; }

        agents = mission.Agents
            .Where(a => a != null && a.IsActive() && a.Team != null && a.Team.IsEnemyOf(mission.PlayerTeam))
            .ToList();
        return true;
    }

    private static int KillAll(List<Agent> agents, out Exception error)
    {
        error = null;
        var killed = 0;
        foreach (var agent in agents)
        {
            if (agent is null || !agent.IsActive())
                continue;
            try
            {
                Kill(agent);
                killed++;
            }
            catch (Exception ex)
            {
                error = ex;
                break;
            }
        }
        return killed;
    }

    internal static void Kill(Agent agent)
    {
        var blow = new Blow(agent.Index)
        {
            DamageType = DamageTypes.Pierce,
            BaseMagnitude = 100000f,
            InflictedDamage = 100000,
            DamagedPercentage = 1f,
            DamageCalculated = true,
            GlobalPosition = agent.Position,
            VictimBodyPart = BoneBodyPartType.Head,
        };
        agent.Die(blow, Agent.KillInfo.Invalid);
    }
}
