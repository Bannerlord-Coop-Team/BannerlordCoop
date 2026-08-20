using Common;
using Common.Logging;
using Common.Messaging;
using Common.Network;
using GameInterface.Services.GameDebug.Messages;
using GameInterface.Services.MapEvents;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace Missions.Battles;

internal class BattleDebugRouteHandler : IHandler
{
    private static readonly ILogger Logger = LogManager.GetLogger<BattleDebugRouteHandler>();
    private static readonly object SiegeFixtureReportGate = new object();
    private static readonly Dictionary<string, NetworkSiegeInteractableFixtureReport> SiegeFixtureReports =
        new Dictionary<string, NetworkSiegeInteractableFixtureReport>();
    private static readonly TimeSpan SiegeFixtureActionRetryWindow = TimeSpan.FromSeconds(15);
    private static readonly TimeSpan SiegeFixtureActionRetryDelay = TimeSpan.FromMilliseconds(100);

    private readonly IMessageBroker messageBroker;
    private readonly INetwork network;
    private readonly CancellationTokenSource disposal = new CancellationTokenSource();
    private static SiegeInteractableFixtureState siegeFixture;

    public BattleDebugRouteHandler(IMessageBroker messageBroker, INetwork network)
    {
        this.messageBroker = messageBroker;
        this.network = network;
        messageBroker.Subscribe<NetworkRouteBattleEnemies>(Handle);
        messageBroker.Subscribe<NetworkSiegeInteractableFixtureAction>(Handle_SiegeInteractableFixtureAction);
        messageBroker.Subscribe<NetworkSiegeInteractableFixtureReport>(Handle_SiegeInteractableFixtureReport);
    }

    public void Dispose()
    {
        disposal.Cancel();
        messageBroker.Unsubscribe<NetworkRouteBattleEnemies>(Handle);
        messageBroker.Unsubscribe<NetworkSiegeInteractableFixtureAction>(Handle_SiegeInteractableFixtureAction);
        messageBroker.Unsubscribe<NetworkSiegeInteractableFixtureReport>(Handle_SiegeInteractableFixtureReport);
        siegeFixture = null;
        lock (SiegeFixtureReportGate)
        {
            SiegeFixtureReports.Clear();
        }
        disposal.Dispose();
    }

    private static void Handle(MessagePayload<NetworkRouteBattleEnemies> payload)
    {
        if (ModInformation.IsServer) return;

        GameThread.RunSafe(() =>
        {
            var mission = Mission.Current;
            var controller = mission?.GetMissionBehavior<CoopBattleController>();
            var playerTeam = mission?.PlayerTeam;
            if (mission == null ||
                controller == null ||
                playerTeam == null ||
                controller.Session.InstanceId != payload.What.MapEventId ||
                !controller.Session.IsLocalHost)
            {
                return;
            }

            var enemySide = playerTeam.Side == BattleSideEnum.Attacker
                ? BattleSideEnum.Defender
                : BattleSideEnum.Attacker;
            var enemies = mission.Agents
                .Where(agent =>
                    agent.IsActive() &&
                    agent.IsHuman &&
                    agent.Team?.Side == enemySide &&
                    !agent.IsRunningAway)
                .ToArray();
            var routeCount = Math.Max(0, enemies.Length - payload.What.EnemiesToLeaveFighting);

            for (int i = 0; i < routeCount; i++)
                enemies[i].Retreat(mission.GetClosestFleePositionForAgent(enemies[i]));

            Logger.Information(
                "[BattleDebug] Ordered {RoutedCount}/{EnemyCount} authoritative enemies to retreat for {MapEventId}",
                routeCount,
                enemies.Length,
                payload.What.MapEventId);
        });
    }

    private void Handle_SiegeInteractableFixtureAction(
        MessagePayload<NetworkSiegeInteractableFixtureAction> payload)
    {
        if (ModInformation.IsServer) return;

        QueueSiegeInteractableFixtureAction(
            payload.What,
            DateTime.UtcNow.Add(SiegeFixtureActionRetryWindow));
    }

    private void Handle_SiegeInteractableFixtureReport(
        MessagePayload<NetworkSiegeInteractableFixtureReport> payload)
    {
        if (ModInformation.IsClient) return;

        var report = payload.What;
        lock (SiegeFixtureReportGate)
        {
            SiegeFixtureReports[ReportKey(report.ControllerId, report.Action)] = report;
            Monitor.PulseAll(SiegeFixtureReportGate);
        }
    }

    private void QueueSiegeInteractableFixtureAction(
        NetworkSiegeInteractableFixtureAction action,
        DateTime deadlineUtc)
    {
        if (disposal.IsCancellationRequested) return;

        GameThread.RunSafe(() =>
        {
            if (disposal.IsCancellationRequested) return;
            if (TryApplySiegeInteractableFixtureAction(action)) return;

            if (DateTime.UtcNow >= deadlineUtc)
            {
                SendFixtureReport(
                    action,
                    machine: null,
                    agent: null,
                    success: false,
                    error: "siege mission did not become ready for the fixture action");
                return;
            }

            Task.Delay(SiegeFixtureActionRetryDelay, disposal.Token).ContinueWith(
                _ => QueueSiegeInteractableFixtureAction(action, deadlineUtc),
                CancellationToken.None,
                TaskContinuationOptions.OnlyOnRanToCompletion,
                TaskScheduler.Default);
        });
    }

    private bool TryApplySiegeInteractableFixtureAction(NetworkSiegeInteractableFixtureAction action)
    {
        var mission = Mission.Current;
        var controller = mission?.GetMissionBehavior<CoopBattleController>();
        if (mission == null || !mission.IsSiegeBattle || controller == null)
        {
            return false;
        }

        var machine = action.Action == SiegeInteractableFixtureAction.Capture
            ? mission.MissionObjects
                .OfType<UsableMachine>()
                .Where(candidate => candidate.GetType().Name.Equals(action.MachineType, StringComparison.Ordinal))
                .OrderBy(candidate => candidate.Id.Id)
                .FirstOrDefault()
            : mission.MissionObjects
                .OfType<UsableMachine>()
                .FirstOrDefault(candidate => candidate.Id.Id == action.MachineId);
        var agent = mission.MainAgent;
        if (agent == null || !agent.IsActive())
        {
            return false;
        }
        if (machine == null)
        {
            SendFixtureReport(action, machine, agent, false, "machine or local main agent unavailable");
            return true;
        }

        try
        {
            switch (action.Action)
            {
                case SiegeInteractableFixtureAction.Capture:
                    siegeFixture = new SiegeInteractableFixtureState(
                        machine.Id.Id,
                        agent.Position,
                        agent.LookDirection,
                        machine is CastleGate captureGate ? (int)captureGate.State : -1);
                    break;
                case SiegeInteractableFixtureAction.Prepare:
                    PrepareLocalInteraction(machine, agent);
                    break;
                case SiegeInteractableFixtureAction.Use:
                    UsePreparedInteraction(machine, agent);
                    break;
                case SiegeInteractableFixtureAction.Stop:
                    if (agent.CurrentlyUsedGameObject != null)
                    {
                        agent.StopUsingGameObject(isSuccessful: true);
                    }
                    break;
                case SiegeInteractableFixtureAction.Restore:
                    RestoreLocalFixture(machine, agent);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(action.Action));
            }

            SendFixtureReport(action, machine, agent, true, string.Empty);
        }
        catch (Exception ex)
        {
            SendFixtureReport(action, machine, agent, false, ex.Message);
        }

        return true;
    }

    private void PrepareLocalInteraction(UsableMachine machine, Agent agent)
    {
        if (siegeFixture == null || siegeFixture.MachineId != machine.Id.Id)
        {
            throw new InvalidOperationException("siege interactable fixture was not captured");
        }

        if (agent.CurrentlyUsedGameObject != null)
        {
            agent.StopUsingGameObject(isSuccessful: true);
        }

        var standingPoint = machine.StandingPoints.FirstOrDefault(agent.CanUseObject);
        if (standingPoint == null)
        {
            throw new InvalidOperationException("no locally eligible standing point was found");
        }

        var frame = standingPoint.GetUserFrameForAgent(agent);
        var direction = frame.Rotation.f.AsVec2.Normalized();
        var offset = new Vec3(direction.x, direction.y, 0f) * 1.25f;
        agent.TeleportToPosition(frame.Origin.GetGroundVec3() - offset);
        agent.LookDirection = new Vec3(direction.x, direction.y, 0f);
        agent.SetMovementDirection(in direction);
        agent.SetTargetPositionAndDirection(frame.Origin.AsVec2, in frame.Rotation.f);
        siegeFixture.StandingPoint = standingPoint;
    }

    private void UsePreparedInteraction(UsableMachine machine, Agent agent)
    {
        if (siegeFixture?.StandingPoint == null || siegeFixture.MachineId != machine.Id.Id)
        {
            throw new InvalidOperationException("siege interactable fixture was not prepared");
        }

        agent.UseGameObject(siegeFixture.StandingPoint);
        if (!ReferenceEquals(agent.CurrentlyUsedGameObject, siegeFixture.StandingPoint))
        {
            throw new InvalidOperationException("local agent did not start using the standing point");
        }
    }

    private void RestoreLocalFixture(UsableMachine machine, Agent agent)
    {
        if (agent.CurrentlyUsedGameObject != null)
        {
            agent.StopUsingGameObject(isSuccessful: true);
        }

        if (siegeFixture != null && siegeFixture.MachineId == machine.Id.Id)
        {
            agent.TeleportToPosition(siegeFixture.OriginalPosition);
            agent.LookDirection = siegeFixture.OriginalLookDirection;
            var movementDirection = siegeFixture.OriginalLookDirection.AsVec2.Normalized();
            agent.SetMovementDirection(in movementDirection);
        }

        RestoreGate(machine, siegeFixture?.OriginalGateState ?? -1);
        siegeFixture = null;
    }

    internal static void RestoreGate(UsableMachine machine, int originalGateState)
    {
        if (!(machine is CastleGate gate) || originalGateState < 0) return;

        SiegeMissionAuthorityGate.SuppressCapture = true;
        try
        {
            if ((CastleGate.GateState)originalGateState == CastleGate.GateState.Open)
            {
                gate.OpenDoor();
            }
            else
            {
                gate.CloseDoor();
            }
        }
        finally
        {
            SiegeMissionAuthorityGate.SuppressCapture = false;
        }
    }

    private void SendFixtureReport(
        NetworkSiegeInteractableFixtureAction action,
        UsableMachine machine,
        Agent agent,
        bool success,
        string error)
    {
        int eligiblePoints = machine == null || agent == null
            ? 0
            : machine.StandingPoints.Count(agent.CanUseObject);
        bool currentlyUsing = machine != null && agent?.CurrentlyUsedGameObject != null
            && machine.StandingPoints.Contains(agent.CurrentlyUsedGameObject);
        int gateState = machine is CastleGate gate ? (int)gate.State : -1;
        bool simulatedLocally = machine != null
            && SiegeMissionAuthorityGate.IsMachineSimulatedLocally(machine.Id.Id);

        network.SendAll(new NetworkSiegeInteractableFixtureReport(
            action.ControllerId,
            machine?.Id.Id ?? action.MachineId,
            action.Action,
            success,
            eligiblePoints,
            currentlyUsing,
            gateState,
            simulatedLocally,
            error ?? string.Empty));
    }

    internal static void ClearSiegeFixtureReport(
        string controllerId,
        SiegeInteractableFixtureAction action)
    {
        lock (SiegeFixtureReportGate)
        {
            SiegeFixtureReports.Remove(ReportKey(controllerId, action));
        }
    }

    internal static bool WaitForSiegeFixtureReport(
        string controllerId,
        SiegeInteractableFixtureAction action,
        TimeSpan timeout,
        out NetworkSiegeInteractableFixtureReport report)
    {
        string key = ReportKey(controllerId, action);
        var deadline = DateTime.UtcNow.Add(timeout);
        lock (SiegeFixtureReportGate)
        {
            while (!SiegeFixtureReports.TryGetValue(key, out report))
            {
                TimeSpan remaining = deadline - DateTime.UtcNow;
                if (remaining <= TimeSpan.Zero) return false;
                Monitor.Wait(SiegeFixtureReportGate, remaining);
            }

            SiegeFixtureReports.Remove(key);
            return true;
        }
    }

    internal static NetworkSiegeInteractableFixtureReport GetLocalSiegeFixtureState()
    {
        var mission = Mission.Current;
        var controller = mission?.GetMissionBehavior<CoopBattleController>();
        var agent = mission?.MainAgent;
        if (mission == null || !mission.IsSiegeBattle || controller == null || agent == null)
        {
            return null;
        }

        var machine = mission.MissionObjects
            .OfType<UsableMachine>()
            .FirstOrDefault(candidate => candidate.Id.Id == siegeFixture?.MachineId);
        if (machine == null) return null;

        int eligiblePoints = machine.StandingPoints.Count(agent.CanUseObject);
        bool currentlyUsing = agent.CurrentlyUsedGameObject != null
            && machine.StandingPoints.Contains(agent.CurrentlyUsedGameObject);
        int gateState = machine is CastleGate gate ? (int)gate.State : -1;
        return new NetworkSiegeInteractableFixtureReport(
            controller.Session.OwnControllerId,
            machine.Id.Id,
            SiegeInteractableFixtureAction.Use,
            true,
            eligiblePoints,
            currentlyUsing,
            gateState,
            SiegeMissionAuthorityGate.IsMachineSimulatedLocally(machine.Id.Id),
            string.Empty);
    }

    private static string ReportKey(string controllerId, SiegeInteractableFixtureAction action)
        => $"{controllerId}:{(int)action}";

    private sealed class SiegeInteractableFixtureState
    {
        public int MachineId { get; }
        public Vec3 OriginalPosition { get; }
        public Vec3 OriginalLookDirection { get; }
        public int OriginalGateState { get; }
        public StandingPoint StandingPoint { get; set; }

        public SiegeInteractableFixtureState(
            int machineId,
            Vec3 originalPosition,
            Vec3 originalLookDirection,
            int originalGateState)
        {
            MachineId = machineId;
            OriginalPosition = originalPosition;
            OriginalLookDirection = originalLookDirection;
            OriginalGateState = originalGateState;
        }
    }
}
