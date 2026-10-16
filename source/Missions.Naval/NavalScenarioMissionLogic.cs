#if DEBUG
using Common;
using Common.Logging;
using GameInterface.Services.MapEvents.NavalScenarios;
using Missions.Battles;
using NavalDLC.GauntletUI.MissionViews;
using NavalDLC.Missions.MissionLogics;
using NavalDLC.Missions.Objects;
using NavalDLC.ViewModelCollection.OrderOfBattle;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
using TaleWorlds.MountAndBlade.GauntletUI.Mission.Singleplayer;
using TaleWorlds.MountAndBlade.Source.Missions.Handlers;

namespace Missions.Naval;

/// <summary>
/// [Game thread] DEBUG: runs this client's steps of the active naval scenario inside its battle: presses Ready on the
/// naval order of battle, then places the own hull and throws a rope, drives the helm, disconnects or retreats, as the
/// scenario says; a client that disconnected presses Ready again in the battle it reconnects into.
/// </summary>
public class NavalScenarioMissionLogic : MissionLogic
{
    private static readonly ILogger Logger = LogManager.GetLogger<NavalScenarioMissionLogic>();

    internal const string DisconnectCommand = "coop.debug.connection.disconnect";
    internal const string ReconnectCommand = "coop.debug.connection.reconnect";

    private const float TargetWaitSeconds = 90f;
    // A teleported hull settles on the water before its rope or helm step.
    private const float SettleSeconds = 2f;
    private const float RopeRetrySeconds = 0.5f;
    private const float RopeGiveUpSeconds = 15f;

    private readonly INavalScenarioState state;
    private readonly INavalDebugHulls hulls;
    private readonly IBattleSession session;
    private readonly INetworkShipRegistry registry;
    private readonly NavalScenarioConfig config;
    private readonly INavalScenarioReadyPress readyPress = new NavalScenarioReadyPress(
        press => GameThread.EnqueueSafe(press, context: "NavalScenarioMissionLogic.PressReady"));
    // The retreat confirmation's OK ends the mission, so its presses run off the mission tick as Ready's do.
    private readonly INavalScenarioReadyPress retreatPress = new NavalScenarioReadyPress(
        press => GameThread.EnqueueSafe(press, context: "NavalScenarioMissionLogic.PressRetreat"));

    private float sinceDeployment;
    private float sincePlacement;
    // This client disconnected out of this mission; its scenario goes on in the mission it reconnects into.
    private bool disconnectedHere;
    private float ropeCooldown;
    private MissionShip placedOwn;
    private MissionShip placedTarget;

    public NavalScenarioMissionLogic(INavalScenarioState state, INavalDebugHulls hulls, IBattleSession session,
        INetworkShipRegistry registry)
    {
        this.state = state;
        this.hulls = hulls;
        this.session = session;
        this.registry = registry;
        config = state.Current;
    }

    public override void OnMissionTick(float dt)
    {
        if (config == null || state.Current != config) return;

        if (Mission.MissionEnded)
        {
            End();
            return;
        }

        if (!Mission.IsDeploymentFinished)
        {
            state.Advance(NavalScenarioPhase.Deploying);
            TickReady(dt);
            return;
        }

        state.Advance(NavalScenarioPhase.Fighting);
        Complete(NavalScenarioState.ReadyStep, true, readyPress.Presses > 0
            ? $"Ready pressed {readyPress.Presses} time(s) on the naval order of battle"
            : "deployment finished without the scenario pressing Ready");
        if (IsRejoining)
            Complete(NavalScenarioState.RejoinStep, true, $"reconnected into the battle and pressed Ready {readyPress.Presses} time(s)");

        sinceDeployment += dt;
        TickTimeline(dt);
    }

    public override void OnRemoveBehavior()
    {
        End();
        base.OnRemoveBehavior();
    }

    // Ready's FinishDeployment removes two behaviors, which breaks Mission.OnTick's reverse index over them, so the
    // press runs after the tick returns, as a click on the order of battle's UI layer does.
    // The mission a disconnected client reconnected into, before its deployment there is over.
    private bool IsRejoining => !disconnectedHere && !state.IsPending(NavalScenarioState.DisconnectStep)
        && state.IsPending(NavalScenarioState.RejoinStep);

    private bool IsPressingReady => state.IsPending(NavalScenarioState.ReadyStep) || IsRejoining;

    private void TickReady(float dt)
    {
        if (!IsPressingReady) return;

        readyPress.Tick(dt, () => ReadyOrderOfBattle() != null, PressReady);
    }

    private void PressReady()
    {
        ReadyOrderOfBattle().ExecuteBeginMission();
        Logger.Information("[NavalScenario] Pressed Ready ({Presses}) for {MapEventId}", readyPress.Presses, config.MapEventId);
    }

    // The order of battle when its Ready button would accept; ExecuteBeginMission refuses while the assignment is
    // dirty or a ship is unmanned. Null once this mission is gone, its deployment finished or the step completed.
    private NavalOrderOfBattleVM ReadyOrderOfBattle()
    {
        var mission = Mission;
        if (mission == null || Mission.Current != mission || mission.MissionEnded || mission.IsDeploymentFinished) return null;
        if (state.Current != config || !IsPressingReady) return null;

        var view = mission.GetMissionBehavior<MissionGauntletNavalOrderOfBattleView>();
        var orderOfBattle = view?._dataSource;
        if (orderOfBattle == null || !IsReadyEnabled(view._isActive, orderOfBattle.IsAssignmentDirty, orderOfBattle.CanStartMission))
            return null;

        return orderOfBattle;
    }

    internal static bool IsReadyEnabled(bool viewActive, bool assignmentDirty, bool canStartMission) =>
        viewActive && !assignmentDirty && canStartMission;

    private void TickTimeline(float dt)
    {
        if (state.IsPending(NavalScenarioState.PlaceStep))
        {
            TryPlace();
            return;
        }

        sincePlacement += dt;
        if (state.IsPending(NavalScenarioState.RopeStep)) TickRope(dt);
        if (state.IsPending(NavalScenarioState.HelmStep) && sincePlacement >= SettleSeconds) DriveHelm();
        if (state.IsPending(NavalScenarioState.SinkStep) && sinceDeployment >= config.DelaySeconds) HitWeakestAiHull();
        if (state.IsPending(NavalScenarioState.DisconnectStep) && sinceDeployment >= config.DelaySeconds) Disconnect();
        if (state.IsPending(NavalScenarioState.RetreatStep) && sinceDeployment >= config.DelaySeconds)
            retreatPress.Tick(dt, () => RetreatHandler() != null, PressRetreat);
    }

    // Tab's scoreboard Retreat button (SPScoreboardVM.OnExitBattle) asks BasicMissionHandler for the confirmation; a later
    // press clicks its OK, which runs OnBattleEnded and Mission.RetreatMission as the player's click does.
    private void PressRetreat()
    {
        var handler = RetreatHandler();
        if (!handler.IsWarningWidgetOpened)
        {
            Mission.GetMissionBehavior<MissionGauntletBattleScore>().DataSource.ExecuteQuitAction();
            if (handler.IsWarningWidgetOpened)
                Logger.Information("[NavalScenario] Pressed Retreat for {MapEventId}; the retreat confirmation is open", config.MapEventId);
            else
                Complete(NavalScenarioState.RetreatStep, false, "Tab's Retreat opened no retreat confirmation");
            return;
        }

        Complete(NavalScenarioState.RetreatStep, true, $"Tab's Retreat opened vanilla's retreat confirmation {sinceDeployment:0} s " +
            "after deploying; accepted it");
        InformationManager.HideInquiry();
        handler.OnEventAcceptSelectionWidget();
    }

    // The retreat handler while this mission runs, its scoreboard exists and the retreat step is pending; null otherwise.
    private BasicMissionHandler RetreatHandler()
    {
        var mission = Mission;
        if (mission == null || Mission.Current != mission || mission.MissionEnded) return null;
        if (state.Current != config || !state.IsPending(NavalScenarioState.RetreatStep)) return null;
        if (mission.GetMissionBehavior<MissionGauntletBattleScore>()?.DataSource == null) return null;

        return mission.GetMissionBehavior<BasicMissionHandler>();
    }

    private void TryPlace()
    {
        string error = hulls.TryGetOwnHull(out var own);
        var targets = error == null ? AfloatAiHulls() : Array.Empty<MissionShip>();
        var shipsLogic = Mission.GetMissionBehavior<NavalShipsLogic>();
        if (error != null || targets.Length == 0 || shipsLogic == null)
        {
            if (sinceDeployment >= TargetWaitSeconds) FailPlacement(error ?? "no afloat AI hull was fielded");
            return;
        }

        int rank = Math.Max(0, Array.IndexOf(config.ControllerIds, session.OwnControllerId));
        var target = targets[rank % targets.Length];
        bool starboard = (rank / targets.Length) % 2 == 0;
        var targetFrame = target.GlobalFrame;
        var pose = PlacementPose(config.Placement, targetFrame.origin.AsVec2, targetFrame.rotation.f.AsVec2,
            own.MissionShipObject.DeploymentArea, target.MissionShipObject.DeploymentArea, config.Spacing, starboard);

        // NavalShipsLogic.TeleportShip snaps the hull to the water and carries its crew, as ShipOrder's deployment move does.
        var frame = MatrixFrame.Identity;
        frame.rotation.f = pose.Direction.ToVec3().NormalizedCopy();
        frame.rotation.OrthonormalizeAccordingToForwardAndKeepUpAsZAxis();
        frame.origin = pose.Position.ToVec3();
        shipsLogic.TeleportShip(own, frame, checkFreeArea: false);

        placedOwn = own;
        placedTarget = target;
        sincePlacement = 0f;
        Complete(NavalScenarioState.PlaceStep, true, $"own hull {hulls.ShipIdOf(own)} placed {config.Placement} " +
            $"{config.Spacing:0.#} m from AI hull {hulls.ShipIdOf(target)}");
    }

    private MissionShip[] AfloatAiHulls() => registry.Ships
        .Where(ship => ship.IsNpcParty)
        .OrderBy(ship => ship.ShipId)
        .Select(ship => ship.Hull as MissionShip)
        .Where(hull => hull != null && !hull.IsRemoved && !hull.IsSinking)
        .ToArray();

    private void FailPlacement(string detail)
    {
        Complete(NavalScenarioState.PlaceStep, false, detail);
        Complete(NavalScenarioState.RopeStep, false, "the hull was not placed");
        Complete(NavalScenarioState.HelmStep, false, "the hull was not placed");
    }

    /// <summary>
    /// The own hull's position and heading for a placement against a target hull, from both hulls' deployment areas
    /// (x width, y length). Boarding lies parallel beside the target; ramming points the bow at the target's side.
    /// </summary>
    internal static (Vec2 Position, Vec2 Direction) PlacementPose(NavalScenarioPlacement placement, Vec2 targetPosition,
        Vec2 targetForward, Vec2 ownArea, Vec2 targetArea, float spacing, bool starboard)
    {
        var forward = targetForward.Normalized();
        var side = starboard ? forward.RightVec() : forward.LeftVec();
        if (placement == NavalScenarioPlacement.Ramming)
        {
            float bowDistance = spacing + (ownArea.y / 2f) + (targetArea.x / 2f);
            return (targetPosition + (side * bowDistance), -side);
        }

        float beamDistance = spacing + (ownArea.x / 2f) + (targetArea.x / 2f);
        return (targetPosition + (side * beamDistance), forward);
    }

    // Each free rope station is tried in order through the rope_throw cheat's path until one reaches an aligned point.
    private void TickRope(float dt)
    {
        ropeCooldown -= dt;
        if (sincePlacement < SettleSeconds || ropeCooldown > 0f) return;
        ropeCooldown = RopeRetrySeconds;

        string last = "the own hull has no rope stations";
        int stations = hulls.RopeStations(placedOwn).Length;
        for (int index = 0; index < stations; index++)
        {
            var result = NavalRopeDebugCommands.Throw(hulls, placedOwn, placedTarget, index, targetIndex: null);
            if (result.Succeeded)
            {
                Complete(NavalScenarioState.RopeStep, true, result.Output);
                return;
            }

            last = result.Output;
        }

        if (sincePlacement >= RopeGiveUpSeconds) Complete(NavalScenarioState.RopeStep, false, last);
    }

    private void DriveHelm()
    {
        var request = new NavalHelmCoopCommand.HelmRequest(0f, 1f, 1, NavalHelmCoopCommand.MaxSeconds);
        var result = NavalHelmCoopCommand.Drive(hulls, request);
        Complete(NavalScenarioState.HelmStep, result.Succeeded, result.Output);
    }

    // The battle host owns the AI hulls, so its hit applies there and replicates as the hull's condition.
    private void HitWeakestAiHull()
    {
        if (!session.IsLocalHost)
        {
            Complete(NavalScenarioState.SinkStep, true, "not the battle host, which hits the AI hull");
            return;
        }

        var target = AfloatAiHulls().OrderBy(hull => hull.HitPoints / hull.ShipOrigin.MaxHitPoints).FirstOrDefault();
        if (target == null)
        {
            Complete(NavalScenarioState.SinkStep, false, "no afloat AI hull was fielded");
            return;
        }

        var result = NavalDamageCoopCommand.Hit(hulls.ShipIdOf(target).ToString(), hull => SinkingHitDamage(hull.ShipOrigin.MaxHitPoints),
            "NAVAL_DAMAGE");
        Complete(NavalScenarioState.SinkStep, result.Succeeded, result.Output);
    }

    internal static float SinkingHitDamage(float maxHitPoints) => maxHitPoints * NavalScenarioPresets.SinkingHitRatio;

    private void Disconnect()
    {
        var disconnecting = config.DisconnectingControllerId(session.HostControllerId);
        if (disconnecting == null || !session.IsOwn(disconnecting))
        {
            var detail = disconnecting == null ? "no player to disconnect" : $"{disconnecting} disconnects, so this client stays connected";
            Complete(NavalScenarioState.DisconnectStep, true, detail);
            Complete(NavalScenarioState.RejoinStep, true, detail);
            return;
        }

        disconnectedHere = true;
        Complete(NavalScenarioState.DisconnectStep, true, $"{(session.IsLocalHost ? "the battle host" : "a player that is not the host")} " +
            $"disconnects now and reconnects in {config.ReconnectSeconds:0} s");
        float reconnectSeconds = config.ReconnectSeconds;

        // Both run as the console would, off the mission tick (RunSafe would run inline here); the reconnect outlives
        // this session's container.
        GameThread.EnqueueSafe(() => RunConsoleCommand(DisconnectCommand), context: nameof(Disconnect));
        Task.Delay(TimeSpan.FromSeconds(reconnectSeconds)).ContinueWith(_ =>
            GameThread.RunSafe(() => RunConsoleCommand(ReconnectCommand), context: nameof(Disconnect)));
    }

    private static void RunConsoleCommand(string command)
    {
        string output = CommandLineFunctionality.CallFunction(command, new List<string>(), out bool found);
        Logger.Information("[NavalScenario] {Command} found={Found}: {Output}", command, found, output);
    }

    private void End()
    {
        if (config == null || state.Current != config || state.Phase == NavalScenarioPhase.Ended || disconnectedHere) return;

        state.Advance(NavalScenarioPhase.Ended);
        foreach (var step in state.Steps.Where(step => step.Status == NavalScenarioStepStatus.Pending).ToArray())
            Complete(step.Name, false, "the battle ended before this step ran");
    }

    private void Complete(string step, bool succeeded, string detail)
    {
        if (!state.IsPending(step)) return;

        state.Complete(step, succeeded, detail);
        Logger.Information("[NavalScenario] Step {Step} {Outcome}: {Detail}", step, succeeded ? "done" : "failed", detail);
    }
}
#endif
