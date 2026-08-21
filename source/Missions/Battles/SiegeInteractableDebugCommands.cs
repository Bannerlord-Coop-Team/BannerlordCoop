using Common;
using Common.Network;
using GameInterface;
using GameInterface.Services.GameDebug.Messages;
using GameInterface.Services.Players;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.MountAndBlade;
using static TaleWorlds.Library.CommandLineFunctionality;

namespace Missions.Battles;

internal static class SiegeInteractableDebugCommands
{
    private static readonly TimeSpan ReportTimeout = TimeSpan.FromSeconds(75);
    private static FixtureState fixture;

    [CommandLineArgumentFunction("siege_interactable_readiness", "coop.debug.battle")]
    public static string Readiness(List<string> args)
    {
        var errors = new List<string>();
        string expectedControllerId = args.Count == 1 ? args[0] : string.Empty;
        if (args.Count != 1 || string.IsNullOrEmpty(expectedControllerId))
        {
            errors.Add("expected-controller-id-required");
        }

        bool isClient = ModInformation.IsClient;
        if (!isClient)
        {
            errors.Add("not-a-client");
        }

        var mission = Mission.Current;
        var controller = mission?.GetMissionBehavior<CoopBattleController>();
        var session = controller?.Session;
        var mainAgent = mission?.MainAgent;
        var deploymentController = mission?.GetMissionBehavior<DeploymentMissionController>();
        bool siegeMission = mission?.IsSiegeBattle == true;
        bool hasBattleController = controller != null;
        bool sessionStarted = session?.HasInstance == true;
        bool joinedSiegeAssault = MobileParty.MainParty?.MapEvent?.IsSiegeAssault == true;
        bool deploymentPhaseActive = deploymentController != null;
        bool deploymentTeamSetupOver = deploymentController?.TeamSetupOver == true;
        bool deploymentCommitted = controller?.Deployment.IsCommitted == true;
        bool deploymentFinished = mission?.IsDeploymentFinished == true;
        bool deploymentComplete = deploymentFinished
            && !deploymentPhaseActive
            && deploymentCommitted;
        bool mainAgentActive = mainAgent?.IsActive() == true;
        bool mainAgentInMission = mainAgent != null && mainAgent.Mission == mission;
        string ownControllerId = session?.OwnControllerId ?? string.Empty;
        bool expectedControllerMatches = ownControllerId == expectedControllerId;
        bool deploymentFinishReady = isClient
            && siegeMission
            && hasBattleController
            && sessionStarted
            && joinedSiegeAssault
            && expectedControllerMatches
            && (deploymentComplete || (deploymentPhaseActive && deploymentTeamSetupOver));
        bool agentRegistered = false;
        bool locallyControlled = false;
        bool agentAuthorityMatchesSession = false;
        string agentControllerId = string.Empty;

        if (!siegeMission)
        {
            errors.Add("siege-mission-unavailable");
        }
        if (!hasBattleController)
        {
            errors.Add("battle-controller-unavailable");
        }
        if (!sessionStarted)
        {
            errors.Add("battle-session-unavailable");
        }
        if (!joinedSiegeAssault)
        {
            errors.Add("siege-assault-not-joined");
        }
        if (!mainAgentActive || !mainAgentInMission)
        {
            errors.Add("local-main-agent-unavailable");
        }
        if (!expectedControllerMatches)
        {
            errors.Add("controller-id-mismatch");
        }
        if (!deploymentComplete)
        {
            errors.Add(deploymentPhaseActive && !deploymentTeamSetupOver
                ? "deployment-team-setup-incomplete"
                : "local-deployment-incomplete");
        }

        if (!ContainerProvider.TryResolve<Missions.INetworkAgentRegistry>(out var registry))
        {
            errors.Add("agent-registry-unavailable");
        }
        else if (mainAgent == null || !registry.TryGetAgentInfo(mainAgent, out var info))
        {
            errors.Add("local-main-agent-unregistered");
        }
        else
        {
            agentRegistered = true;
            locallyControlled = registry.IsLocallyControlled(info.AgentId);
            agentControllerId = info.CurrentAuthority ?? string.Empty;
            agentAuthorityMatchesSession = agentControllerId == ownControllerId;
            if (!locallyControlled)
            {
                errors.Add("local-main-agent-not-locally-controlled");
            }
            if (!agentAuthorityMatchesSession)
            {
                errors.Add("local-main-agent-controller-mismatch");
            }
        }

        bool ready = errors.Count == 0;
        return Structured(new
        {
            ready,
            expectedControllerId,
            ownControllerId,
            agentControllerId,
            missionInstanceId = session?.InstanceId ?? string.Empty,
            isClient,
            siegeMission,
            hasBattleController,
            sessionStarted,
            joinedSiegeAssault,
            deploymentPhaseActive,
            deploymentTeamSetupOver,
            deploymentCommitted,
            deploymentFinished,
            deploymentComplete,
            deploymentFinishReady,
            mainAgentActive,
            mainAgentInMission,
            expectedControllerMatches,
            agentRegistered,
            locallyControlled,
            agentAuthorityMatchesSession,
            errors = errors.ToArray(),
        });
    }

#if DEBUG
    [CommandLineArgumentFunction("siege_interactable_finish_deployment", "coop.debug.battle")]
    public static string FinishDeployment(List<string> args)
    {
        string expectedControllerId = args.Count == 1 ? args[0] : string.Empty;
        var errors = new List<string>();
        if (args.Count != 1 || string.IsNullOrEmpty(expectedControllerId))
        {
            errors.Add("expected-controller-id-required");
        }

        bool isClient = ModInformation.IsClient;
        if (!isClient)
        {
            errors.Add("not-a-client");
        }

        var mission = Mission.Current;
        var controller = mission?.GetMissionBehavior<CoopBattleController>();
        var session = controller?.Session;
        string ownControllerId = session?.OwnControllerId ?? string.Empty;
        bool siegeMission = mission?.IsSiegeBattle == true;
        bool hasBattleController = controller != null;
        bool sessionStarted = session?.HasInstance == true;
        bool joinedSiegeAssault = MobileParty.MainParty?.MapEvent?.IsSiegeAssault == true;
        bool expectedControllerMatches = ownControllerId == expectedControllerId;
        var deploymentController = mission?.GetMissionBehavior<DeploymentMissionController>();
        bool deploymentPhaseActive = deploymentController != null;
        bool deploymentTeamSetupOver = deploymentController?.TeamSetupOver == true;
        bool deploymentCommitted = controller?.Deployment.IsCommitted == true;
        bool deploymentFinished = mission?.IsDeploymentFinished == true;
        bool deploymentComplete = deploymentFinished
            && !deploymentPhaseActive
            && deploymentCommitted;

        if (!siegeMission)
        {
            errors.Add("siege-mission-unavailable");
        }
        if (!hasBattleController)
        {
            errors.Add("battle-controller-unavailable");
        }
        if (!sessionStarted)
        {
            errors.Add("battle-session-unavailable");
        }
        if (!joinedSiegeAssault)
        {
            errors.Add("siege-assault-not-joined");
        }
        if (!expectedControllerMatches)
        {
            errors.Add("controller-id-mismatch");
        }

        if (errors.Count != 0)
        {
            return DeploymentCompletionResult(
                false,
                string.Join(",", errors),
                expectedControllerId,
                false);
        }

        if (deploymentComplete)
        {
            return DeploymentCompletionResult(
                true,
                string.Empty,
                expectedControllerId,
                true);
        }

        if (!deploymentTeamSetupOver)
        {
            return DeploymentCompletionResult(
                false,
                "deployment-team-setup-incomplete",
                expectedControllerId,
                false);
        }

        var deploymentHandler = mission.GetMissionBehavior<DeploymentHandler>();
        if (deploymentHandler == null)
        {
            return DeploymentCompletionResult(
                false,
                "deployment-handler-unavailable",
                expectedControllerId,
                false);
        }

        try
        {
            deploymentHandler.FinishDeployment();
        }
        catch (Exception exception)
        {
            return DeploymentCompletionResult(
                false,
                exception.Message,
                expectedControllerId,
                false);
        }

        deploymentComplete = mission.IsDeploymentFinished
            && mission.GetMissionBehavior<DeploymentMissionController>() == null
            && controller.Deployment.IsCommitted;
        return DeploymentCompletionResult(
            deploymentComplete,
            deploymentComplete ? string.Empty : "deployment-finish-did-not-complete",
            expectedControllerId,
            false);
    }
#endif

    [CommandLineArgumentFunction("siege_interactable_capture", "coop.debug.battle")]
    public static string Capture(List<string> args)
    {
        if (ModInformation.IsClient)
            return Failure("This command can only be used by the server");
        if (args.Count != 3)
            return Failure("Usage: coop.debug.battle.siege_interactable_capture <machineType> <firstControllerId> <secondControllerId>");
        if (!ContainerProvider.TryResolve<BattleDebugRouteHandler>(out var routeHandler)
            || !ContainerProvider.TryResolve<INetwork>(out var network)
            || !ContainerProvider.TryResolve<IPlayerManager>(out var playerManager))
        {
            return Failure("Unable to resolve campaign network services");
        }

        fixture = new FixtureState(
            args[0],
            new[] { args[1], args[2] });

#if DEBUG
        return CaptureDebugFixture(routeHandler, network, playerManager);
#else
        foreach (string controllerId in fixture.ControllerIds)
        {
            if (!SendFixtureAction(
                routeHandler,
                network,
                playerManager,
                controllerId,
                SiegeInteractableFixtureAction.Capture))
            {
                fixture = null;
                return Failure($"Unable to capture siege interactable fixture on {controllerId}");
            }
        }

        var captures = new List<object>();
        DateTime captureDeadline = DateTime.UtcNow.Add(ReportTimeout);
        foreach (string controllerId in fixture.ControllerIds)
        {
            if (!WaitForFixtureReport(
                routeHandler,
                controllerId,
                SiegeInteractableFixtureAction.Capture,
                captureDeadline - DateTime.UtcNow,
                out var report))
            {
                fixture = null;
                return Failure($"Timed out capturing siege interactable fixture on {controllerId}");
            }
            if (!report.Success)
            {
                fixture = null;
                return Failure($"Failed to capture siege interactable fixture on {controllerId}: {report.Error}");
            }
            fixture.CaptureReports[controllerId] = report;
            captures.Add(ReportResult(report));
        }

        var firstCapture = fixture.CaptureReports[fixture.ControllerIds[0]];
        bool capturesMatch = fixture.CaptureReports.Values.All(report =>
            report.MachineId == firstCapture.MachineId
            && report.GateState == firstCapture.GateState);
        if (!capturesMatch)
        {
            fixture = null;
            return Failure("Clients captured different siege interactable state");
        }

        fixture.MachineId = firstCapture.MachineId;
        fixture.OriginalGateState = firstCapture.GateState;

        return Structured(new
        {
            success = true,
            error = string.Empty,
            machineId = fixture.MachineId,
            machineType = fixture.MachineType,
            originalGateState = fixture.OriginalGateState,
            controllers = fixture.ControllerIds,
            captures,
        });
#endif
    }

    [CommandLineArgumentFunction("siege_interactable_action", "coop.debug.battle")]
    public static string Action(List<string> args)
    {
        if (ModInformation.IsClient)
            return Failure("This command can only be used by the server");
        if (args.Count != 2 || !Enum.TryParse(args[1], ignoreCase: true, out SiegeInteractableFixtureAction action)
            || action == SiegeInteractableFixtureAction.Capture || action == SiegeInteractableFixtureAction.Restore)
        {
            return Failure("Usage: coop.debug.battle.siege_interactable_action <controllerId> <prepare|use|stop>");
        }
        if (fixture == null) return Failure("No siege interactable fixture is active");
        if (!fixture.ControllerIds.Contains(args[0])) return Failure($"Controller {args[0]} is not part of the fixture");
        if (!ContainerProvider.TryResolve<BattleDebugRouteHandler>(out var routeHandler)
            || !ContainerProvider.TryResolve<INetwork>(out var network)
            || !ContainerProvider.TryResolve<IPlayerManager>(out var playerManager))
        {
            return Failure("Unable to resolve campaign network services");
        }

        if (!SendFixtureAction(routeHandler, network, playerManager, args[0], action)
            || !WaitForFixtureReport(routeHandler, args[0], action, ReportTimeout, out var report))
            return Failure($"Timed out waiting for {action} on {args[0]}");

        return Structured(ReportResult(report));
    }

    [CommandLineArgumentFunction("siege_interactable_state", "coop.debug.battle")]
    public static string State(List<string> args)
    {
        if (args.Count != 0)
            return Failure("Usage: coop.debug.battle.siege_interactable_state");
        if (ModInformation.IsServer)
            return Failure("This command can only be used by a client");

        var report = BattleDebugRouteHandler.GetLocalSiegeFixtureState();
        return report == null
            ? Failure("No local siege interactable fixture is active")
            : Structured(ReportResult(report));
    }

    [CommandLineArgumentFunction("siege_interactable_restore", "coop.debug.battle")]
    public static string Restore(List<string> args)
    {
        if (ModInformation.IsClient)
            return Failure("This command can only be used by the server");
        if (args.Count != 0)
            return Failure("Usage: coop.debug.battle.siege_interactable_restore");
        if (fixture == null) return Failure("No siege interactable fixture is active");
        if (!ContainerProvider.TryResolve<BattleDebugRouteHandler>(out var routeHandler)
            || !ContainerProvider.TryResolve<INetwork>(out var network)
            || !ContainerProvider.TryResolve<IPlayerManager>(out var playerManager))
        {
            return Failure("Unable to resolve campaign network services");
        }

        var restoreErrors = new List<string>();
        var restoreControllerIds = new List<string>();
        foreach (string controllerId in fixture.ControllerIds)
        {
            if (!SendFixtureAction(
                routeHandler,
                network,
                playerManager,
                controllerId,
                SiegeInteractableFixtureAction.Restore))
            {
                restoreErrors.Add($"unable to send restore to {controllerId}");
                continue;
            }

            restoreControllerIds.Add(controllerId);
        }

        DateTime restoreDeadline = DateTime.UtcNow.Add(ReportTimeout);
        foreach (string controllerId in restoreControllerIds)
        {
            if (!WaitForFixtureReport(
                routeHandler,
                controllerId,
                SiegeInteractableFixtureAction.Restore,
                restoreDeadline - DateTime.UtcNow,
                out var report))
            {
                restoreErrors.Add($"timed out on {controllerId}");
                continue;
            }
            if (!report.Success)
            {
                restoreErrors.Add($"failed on {controllerId}: {report.Error}");
                continue;
            }
            fixture.RestoreReports[controllerId] = report;
        }

        if (restoreErrors.Count != 0)
            return Failure("Failed to restore siege interactable fixture: " + string.Join("; ", restoreErrors));

        return Structured(new
        {
            success = true,
            error = string.Empty,
            fixture.MachineId,
            restoredControllers = fixture.RestoreReports.Keys.OrderBy(id => id).ToArray(),
            gateStates = fixture.RestoreReports.Values.Select(report => report.GateState).ToArray(),
        });
    }

    [CommandLineArgumentFunction("siege_interactable_verify", "coop.debug.battle")]
    public static string Verify(List<string> args)
    {
        if (ModInformation.IsClient)
            return Failure("This command can only be used by the server");
        if (args.Count != 0)
            return Failure("Usage: coop.debug.battle.siege_interactable_verify");
        if (fixture == null) return Failure("No siege interactable fixture is active");

        bool controllersRestored = fixture.ControllerIds.All(controllerId =>
            fixture.RestoreReports.TryGetValue(controllerId, out var report)
            && report.Success
            && !report.CurrentlyUsing
            && report.GateState == fixture.OriginalGateState);
        if (!controllersRestored)
        {
            return Failure("Siege interactable fixture restoration failed");
        }

        fixture = null;
        return Structured(new
        {
            success = true,
            error = string.Empty,
        });
    }

    private static bool SendFixtureAction(
        BattleDebugRouteHandler routeHandler,
        INetwork network,
        IPlayerManager playerManager,
        string controllerId,
        SiegeInteractableFixtureAction action)
    {
        if (!playerManager.TryGetPeer(controllerId, out var peer))
        {
            return false;
        }

        BattleDebugRouteHandler.ClearSiegeFixtureReport(controllerId, action);
        network.Send(peer, new NetworkSiegeInteractableFixtureAction(
            controllerId,
            fixture.MachineId,
            action,
            fixture.OriginalGateState,
            fixture.MachineType));
#if DEBUG
        if (action == SiegeInteractableFixtureAction.Capture)
        {
            fixture.CaptureRequestControllerIds.Add(controllerId);
        }
#endif
        return true;
    }

    private static bool WaitForFixtureReport(
        BattleDebugRouteHandler routeHandler,
        string controllerId,
        SiegeInteractableFixtureAction action,
        TimeSpan timeout,
        out NetworkSiegeInteractableFixtureReport report)
    {
        bool received = BattleDebugRouteHandler.WaitForSiegeFixtureReport(
            controllerId,
            action,
            timeout,
            out report);
        GC.KeepAlive(routeHandler);
        return received;
    }

    private static object ReportResult(NetworkSiegeInteractableFixtureReport report)
    {
        return new
        {
            success = report.Success,
            error = report.Error ?? string.Empty,
            report.ControllerId,
            report.MachineId,
            action = report.Action.ToString(),
            report.EligiblePoints,
            report.CurrentlyUsing,
            report.GateState,
            report.SimulatedLocally,
        };
    }

#if DEBUG
    private static string CaptureDebugFixture(
        BattleDebugRouteHandler routeHandler,
        INetwork network,
        IPlayerManager playerManager)
    {
        var captures = new List<object>();
        foreach (string controllerId in fixture.ControllerIds)
        {
            if (!SendFixtureAction(
                routeHandler,
                network,
                playerManager,
                controllerId,
                SiegeInteractableFixtureAction.Capture))
            {
                return FailDebugCapture(
                    routeHandler,
                    network,
                    playerManager,
                    $"Unable to capture siege interactable fixture on {controllerId}");
            }

            if (!WaitForFixtureReport(
                routeHandler,
                controllerId,
                SiegeInteractableFixtureAction.Capture,
                ReportTimeout,
                out var report))
            {
                return FailDebugCapture(
                    routeHandler,
                    network,
                    playerManager,
                    $"Timed out capturing siege interactable fixture on {controllerId}");
            }

            fixture.CaptureReports[controllerId] = report;
            if (!report.Success)
            {
                return FailDebugCapture(
                    routeHandler,
                    network,
                    playerManager,
                    $"Failed to capture siege interactable fixture on {controllerId}: {report.Error}");
            }

            if (fixture.MachineId < 0)
            {
                fixture.MachineId = report.MachineId;
                fixture.OriginalGateState = report.GateState;
            }
            else if (report.MachineId != fixture.MachineId || report.GateState != fixture.OriginalGateState)
            {
                return FailDebugCapture(
                    routeHandler,
                    network,
                    playerManager,
                    "Clients captured different siege interactable state");
            }

            captures.Add(ReportResult(report));
        }

        return Structured(new
        {
            success = true,
            error = string.Empty,
            machineId = fixture.MachineId,
            machineType = fixture.MachineType,
            originalGateState = fixture.OriginalGateState,
            controllers = fixture.ControllerIds,
            captures,
        });
    }

    private static string FailDebugCapture(
        BattleDebugRouteHandler routeHandler,
        INetwork network,
        IPlayerManager playerManager,
        string error)
    {
        string restoreError = RestoreCapturedDebugFixtures(routeHandler, network, playerManager);
        fixture = null;
        return string.IsNullOrEmpty(restoreError)
            ? Failure(error)
            : Failure(error + "; " + restoreError);
    }

    private static string RestoreCapturedDebugFixtures(
        BattleDebugRouteHandler routeHandler,
        INetwork network,
        IPlayerManager playerManager)
    {
        var errors = new List<string>();
        foreach (string controllerId in fixture.CaptureRequestControllerIds.OrderBy(id => id))
        {
            if (fixture.CaptureReports.TryGetValue(controllerId, out var capture)
                && capture.MachineId > 0)
            {
                fixture.MachineId = capture.MachineId;
                fixture.OriginalGateState = capture.GateState;
            }

            if (!SendFixtureAction(
                routeHandler,
                network,
                playerManager,
                controllerId,
                SiegeInteractableFixtureAction.Restore))
            {
                errors.Add($"unable to send capture cleanup to {controllerId}");
                continue;
            }

            if (!WaitForFixtureReport(
                routeHandler,
                controllerId,
                SiegeInteractableFixtureAction.Restore,
                ReportTimeout,
                out var restore))
            {
                errors.Add($"timed out cleaning up {controllerId}");
                continue;
            }

            if (!restore.Success)
            {
                errors.Add($"failed cleaning up {controllerId}: {restore.Error}");
                continue;
            }

            fixture.RestoreReports[controllerId] = restore;
        }

        return string.Join("; ", errors);
    }

    private static string DeploymentCompletionResult(
        bool success,
        string error,
        string expectedControllerId,
        bool alreadyCompleted)
    {
        var mission = Mission.Current;
        var controller = mission?.GetMissionBehavior<CoopBattleController>();
        var session = controller?.Session;
        var deploymentController = mission?.GetMissionBehavior<DeploymentMissionController>();
        var mainAgent = mission?.MainAgent;
        bool deploymentPhaseActive = deploymentController != null;
        bool deploymentTeamSetupOver = deploymentController?.TeamSetupOver == true;
        bool deploymentCommitted = controller?.Deployment.IsCommitted == true;
        bool deploymentFinished = mission?.IsDeploymentFinished == true;
        bool deploymentComplete = deploymentFinished
            && !deploymentPhaseActive
            && deploymentCommitted;
        string ownControllerId = session?.OwnControllerId ?? string.Empty;
        return Structured(new
        {
            success,
            error,
            expectedControllerId,
            ownControllerId,
            siegeMission = mission?.IsSiegeBattle == true,
            hasBattleController = controller != null,
            sessionStarted = session?.HasInstance == true,
            joinedSiegeAssault = MobileParty.MainParty?.MapEvent?.IsSiegeAssault == true,
            expectedControllerMatches = ownControllerId == expectedControllerId,
            deploymentPhaseActive,
            deploymentTeamSetupOver,
            deploymentCommitted,
            deploymentFinished,
            deploymentComplete,
            alreadyCompleted,
            mainAgentActive = mainAgent?.IsActive() == true,
            mainAgentInMission = mainAgent != null && mainAgent.Mission == mission,
        });
    }
#endif

    private static string Structured(object value)
        => "LIVE_TEST_JSON=" + JsonConvert.SerializeObject(value);

    private static string Failure(string error)
        => Structured(new { success = false, error });

    private sealed class FixtureState
    {
        public int MachineId { get; set; } = -1;
        public string MachineType { get; }
        public int OriginalGateState { get; set; } = -1;
        public string[] ControllerIds { get; }
#if DEBUG
        public HashSet<string> CaptureRequestControllerIds { get; } =
            new HashSet<string>();
#endif
        public Dictionary<string, NetworkSiegeInteractableFixtureReport> CaptureReports { get; } =
            new Dictionary<string, NetworkSiegeInteractableFixtureReport>();
        public Dictionary<string, NetworkSiegeInteractableFixtureReport> RestoreReports { get; } =
            new Dictionary<string, NetworkSiegeInteractableFixtureReport>();

        public FixtureState(string machineType, string[] controllerIds)
        {
            MachineType = machineType;
            ControllerIds = controllerIds;
        }
    }
}
