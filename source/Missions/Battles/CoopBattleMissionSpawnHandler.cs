using System;
using Common.Logging;
using Common.Messaging;
using GameInterface;
using GameInterface.Services.GameDebug.Messages;
using GameInterface.Services.MapEvents;
using GameInterface.Services.MapEvents.TroopSupply;
using Missions.Messages;
using Newtonsoft.Json;
using SandBox.Missions.MissionLogics;
using Serilog;
using System.Collections.Generic;
using System.Diagnostics;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;

namespace Missions.Battles;

#if DEBUG
public sealed class RejoinSizingDebugRecord
{
    public string Phase { get; }
    public int ProcessId { get; }
    public string BattleInstanceId { get; }
    public string LocalControllerId { get; }
    public string PlayerSide { get; }
    public string PlayerPartyId { get; }
    public bool DefenderPopulated { get; }
    public bool AttackerPopulated { get; }
    public int DefenderOwned { get; }
    public int AttackerOwned { get; }
    public int BattleSize { get; }
    public bool HasAnyOwnedTroops { get; }
    public bool HasValidBattleSize { get; }
    public bool HasValidMissionSizing { get; }
    public bool HasLocalPlayerOrigin { get; }
    public float HeldSeconds { get; }
    public float HoldDeadlineSeconds { get; }
    public bool InvalidBattleAbortRequested { get; }
    public bool WillEndMission { get; }
    public RejoinPlayerAgentDebugState PlayerAgents { get; }
    public RejoinDeploymentSetupDebugState DeploymentSetup { get; }
    public CoopTroopSupplierDebugObservation SupplierObservation { get; }
    public float MissionTime { get; }
    public DateTime CapturedUtc { get; }
    public RejoinResultTransitionDebugState ResultTransition { get; }

    public RejoinSizingDebugRecord(
        string phase,
        int processId,
        string battleInstanceId,
        string localControllerId,
        BattleSideEnum playerSide,
        string playerPartyId,
        bool defenderPopulated,
        bool attackerPopulated,
        int defenderOwned,
        int attackerOwned,
        int battleSize,
        bool hasAnyOwnedTroops,
        bool hasValidBattleSize,
        bool hasValidMissionSizing,
        bool hasLocalPlayerOrigin,
        float heldSeconds,
        float holdDeadlineSeconds,
        bool invalidBattleAbortRequested,
        bool willEndMission,
        RejoinPlayerAgentDebugState playerAgents,
        RejoinDeploymentSetupDebugState deploymentSetup,
        CoopTroopSupplierDebugObservation supplierObservation,
        float missionTime,
        DateTime capturedUtc,
        RejoinResultTransitionDebugState resultTransition)
    {
        Phase = phase;
        ProcessId = processId;
        BattleInstanceId = battleInstanceId;
        LocalControllerId = localControllerId;
        PlayerSide = playerSide.ToString();
        PlayerPartyId = playerPartyId;
        DefenderPopulated = defenderPopulated;
        AttackerPopulated = attackerPopulated;
        DefenderOwned = defenderOwned;
        AttackerOwned = attackerOwned;
        BattleSize = battleSize;
        HasAnyOwnedTroops = hasAnyOwnedTroops;
        HasValidBattleSize = hasValidBattleSize;
        HasValidMissionSizing = hasValidMissionSizing;
        HasLocalPlayerOrigin = hasLocalPlayerOrigin;
        HeldSeconds = heldSeconds;
        HoldDeadlineSeconds = holdDeadlineSeconds;
        InvalidBattleAbortRequested = invalidBattleAbortRequested;
        WillEndMission = willEndMission;
        PlayerAgents = playerAgents;
        DeploymentSetup = deploymentSetup;
        SupplierObservation = supplierObservation;
        MissionTime = missionTime;
        CapturedUtc = capturedUtc;
        ResultTransition = resultTransition;
    }
}

public sealed class RejoinMissionAgentDebugState
{
    public string Role { get; }
    public bool Present { get; }
    public bool Active { get; }
    public bool InCurrentMission { get; }
    public int Index { get; }
    public string Controller { get; }
    public bool RegistryAvailable { get; }
    public bool Registered { get; }
    public string NetworkAgentId { get; }
    public string CurrentAuthority { get; }
    public string OriginalOwner { get; }
    public string Error { get; }

    public RejoinMissionAgentDebugState(
        string role,
        bool present,
        bool active,
        bool inCurrentMission,
        int index,
        string controller,
        bool registryAvailable,
        bool registered,
        string networkAgentId,
        string currentAuthority,
        string originalOwner,
        string error)
    {
        Role = role;
        Present = present;
        Active = active;
        InCurrentMission = inCurrentMission;
        Index = index;
        Controller = controller;
        RegistryAvailable = registryAvailable;
        Registered = registered;
        NetworkAgentId = networkAgentId;
        CurrentAuthority = currentAuthority;
        OriginalOwner = originalOwner;
        Error = error;
    }
}

public sealed class RejoinDeploymentSetupDebugState
{
    public bool DeploymentControllerPresent { get; }
    public bool? TeamSetupOver { get; }
    public bool SpawnHandlerSized { get; }
    public string VanillaSetupState { get; }
    public string Error { get; }

    public RejoinDeploymentSetupDebugState(
        bool deploymentControllerPresent,
        bool? teamSetupOver,
        bool spawnHandlerSized,
        string vanillaSetupState,
        string error)
    {
        DeploymentControllerPresent = deploymentControllerPresent;
        TeamSetupOver = teamSetupOver;
        SpawnHandlerSized = spawnHandlerSized;
        VanillaSetupState = vanillaSetupState;
        Error = error;
    }
}

public sealed class RejoinPlayerAgentDebugState
{
    public string LocalControllerId { get; }
    public RejoinMissionAgentDebugState InitialPlayerAgent { get; }
    public RejoinMissionAgentDebugState MainAgent { get; }
    public bool? SameAgentInstance { get; }
    public string Relationship { get; }
    public string RegistryError { get; }

    public RejoinPlayerAgentDebugState(
        string localControllerId,
        RejoinMissionAgentDebugState initialPlayerAgent,
        RejoinMissionAgentDebugState mainAgent,
        bool? sameAgentInstance,
        string relationship,
        string registryError)
    {
        LocalControllerId = localControllerId;
        InitialPlayerAgent = initialPlayerAgent;
        MainAgent = mainAgent;
        SameAgentInstance = sameAgentInstance;
        Relationship = relationship;
        RegistryError = registryError;
    }
}

public sealed class RejoinSizingDebugError
{
    public string Phase { get; }
    public int ProcessId { get; }
    public string BattleInstanceId { get; }
    public string LocalControllerId { get; }
    public float MissionTime { get; }
    public string Error { get; }
    public DateTime CapturedUtc { get; }

    public RejoinSizingDebugError(
        string phase,
        int processId,
        string battleInstanceId,
        string localControllerId,
        float missionTime,
        string error,
        DateTime capturedUtc)
    {
        Phase = phase;
        ProcessId = processId;
        BattleInstanceId = battleInstanceId;
        LocalControllerId = localControllerId;
        MissionTime = missionTime;
        Error = error;
        CapturedUtc = capturedUtc;
    }
}

public sealed class RejoinSizingDebugState
{
    public RejoinSizingDebugRecord FirstHeldSizing { get; }
    public RejoinSizingDebugRecord InvalidOriginAbort { get; }
    public RejoinSizingDebugError[] DiagnosticErrors { get; }

    public RejoinSizingDebugState(
        RejoinSizingDebugRecord firstHeldSizing,
        RejoinSizingDebugRecord invalidOriginAbort,
        RejoinSizingDebugError[] diagnosticErrors)
    {
        FirstHeldSizing = firstHeldSizing;
        InvalidOriginAbort = invalidOriginAbort;
        DiagnosticErrors = diagnosticErrors;
    }
}
#endif

/// <summary>
/// Coop replacement for <see cref="SandBoxBattleMissionSpawnHandler"/>: sizes each side to what THIS client's
/// supplier owns (its party, plus the AI/enemy side for the host), not the full side the native handler waits on
/// and never fills. The engine's battle-size cap and wave split are joint across both sides, so both are sized in
/// one pass once both reserves land: at <see cref="AfterStart"/> if already present, else held at zero until
/// <see cref="OnMissionTick"/> sees them, so a late side ends up identical to an on-time one.
/// </summary>
public class CoopBattleMissionSpawnHandler : SandBoxMissionSpawnHandler
{
    private static readonly ILogger Logger = LogManager.GetLogger<CoopBattleMissionSpawnHandler>();
    internal const string InvalidPlayerReserveMessage = "Unable to start the battle because your party's troop reserve was not received. Returning to the campaign map.";

    // Hold this long for a still-in-flight reserve before sizing with whatever landed. A dropped or server-rejected
    // reserve request would otherwise never populate a supplier, and the deployment controller (which gates on
    // IsSized) would wedge on the loading screen forever. A partial response degrades to a one-sided battle; a
    // zero-troop response cannot produce a valid battle and is terminated through the normal mission lifecycle.
    private const float ReserveHoldDeadlineSeconds = 15f;

    private readonly CoopTroopSupplier _defenderSupplier;
    private readonly CoopTroopSupplier _attackerSupplier;
    private readonly IMessageBroker _messageBroker;
    private readonly BattleSideEnum _playerSide;
    private readonly bool _isSallyOut;

    // Latched once the sides are sized jointly; both are held at zero until then.
    private bool _sized;
    private long _appliedAllocationRevision;

    // Time spent holding both sides while a reserve is in flight (only accrues on the held path).
    private float _heldSeconds;
    private bool _invalidBattleAbortRequested;
#if DEBUG
    private readonly object rejoinSizingDebugLock = new object();
    private RejoinSizingDebugRecord firstHeldSizing;
    private RejoinSizingDebugRecord invalidOriginAbort;
    private readonly List<RejoinSizingDebugError> rejoinSizingErrors = new List<RejoinSizingDebugError>();
#endif

    // Gated on by CoopBattleDeploymentMissionController: a game-thread latch, not the suppliers' network-thread
    // IsPopulated (which could read true mid-frame before Init has actually sized).
    public bool IsSized => _sized;

    public CoopBattleMissionSpawnHandler(CoopTroopSupplier defenderSupplier, CoopTroopSupplier attackerSupplier,
        IMessageBroker messageBroker, BattleSideEnum playerSide, bool isSallyOut = false)
    {
        _defenderSupplier = defenderSupplier;
        _attackerSupplier = attackerSupplier;
        _messageBroker = messageBroker;
        _playerSide = playerSide;
        _isSallyOut = isSallyOut;
    }

    public override void AfterStart()
    {
        _missionAgentSpawnLogic.SetSpawnHorses(BattleSideEnum.Defender, !_mapEvent.IsSiegeAssault);
        _missionAgentSpawnLogic.SetSpawnHorses(BattleSideEnum.Attacker,
            !_mapEvent.IsSiegeAssault && !_isSallyOut);

        var sizing = ReadSizing();

        if (sizing.Ready)
        {
            if (sizing.SizeNow && HasValidMissionSizing(sizing) && HasLocalPlayerOrigin())
            {
                // On-time (common): both reserves present, so size before the first tick.
                RunJointInit(sizing);
                _sized = true;
                Logger.Information("[BattleSync] Coop spawn sized on start: Defender={Def}, Attacker={Atk}", sizing.DefenderOwned, sizing.AttackerOwned);
                return;
            }

            // Wait for the retained hero when its origin was already supplied, within the existing deadline.
            AddHeldPhases();
            Logger.Error("[BattleSync] Battle reserves cannot produce valid mission sizing and a local player origin; holding deployment before aborting the invalid mission");
            return;
        }

        // A reserve is still in flight — hold both sides at zero until OnMissionTick sizes them (or the deadline).
        AddHeldPhases();
        Logger.Warning("[BattleSync] Coop spawn handler started before reserves arrived (Def populated={Def}, Atk populated={Atk}) — sizing on tick once both land",
            sizing.DefenderPopulated, sizing.AttackerPopulated);
    }

    // Size once both suppliers populate, then latch. If a reserve never lands, size a usable partial response
    // after ReserveHoldDeadlineSeconds; if no combatant exists, end the invalid mission instead. A mid-battle
    // migration re-feed re-populates an already-sized supplier and is left to ReinforcementFielder, which can
    // distinguish newly-owned parties with no adopted live agents without disturbing the initial phase sizing.
    public override void OnMissionTick(float dt)
    {
        if (_sized)
            ReconcileRefreshedAllocation();

        base.OnMissionTick(dt);
        if (_sized || _invalidBattleAbortRequested) return;

        _heldSeconds += dt;
        var sizing = ReadSizing();
        bool shouldContinueHolding = ShouldContinueHolding(sizing);
#if DEBUG
        if (shouldContinueHolding)
            TryRecordFirstHeldSizing(sizing);
#endif
        if (shouldContinueHolding) return;

        if (!HasValidMissionSizing(sizing) || !HasLocalPlayerOrigin())
        {
            AbortInvalidBattle(sizing);
            return;
        }

        AcceptMissingReserveSides(sizing);

        // Ready, or the deadline expired with a partial/missing reserve. At least one combatant exists here,
        // so the joint Init cannot hit its invalid 0/0 split.
        RunJointInit(sizing);
        LogSizingCompleted(sizing);
        _sized = true;
    }

    private bool ShouldContinueHolding(SideSizing sizing)
    {
        return _heldSeconds < ReserveHoldDeadlineSeconds
            && (!sizing.Ready || !sizing.HasAnyOwnedTroops || !HasLocalPlayerOrigin());
    }

    private bool HasValidMissionSizing(SideSizing sizing)
    {
        if (!sizing.HasValidBattleSize) return false;
        if (!_isSallyOut) return true;

        var targets = CalculateSallyOutSizing(
            sizing.DefenderOwned, sizing.AttackerOwned, sizing.BattleSize);
        return targets.DefenderTotal + targets.AttackerTotal > 0;
    }

    // The native deployment controller dereferences InitialPlayerAgent after spawning. Without the local
    // player's authoritative origin, keep IsSized false and end through the attached mission lifecycle.
    private void AbortInvalidBattle(SideSizing sizing)
    {
#if DEBUG
        TryRecordInvalidOriginAbort(sizing);
#endif
        _invalidBattleAbortRequested = true;
        var playerPartyId = GetLocalPlayerPartyId();
        Logger.Error("[BattleSync] Local player origin missing from battle reserves (side={Side}, party={PartyId}, Def populated={DefP}, Atk populated={AtkP}); ending invalid mission",
            _playerSide, playerPartyId, sizing.DefenderPopulated, sizing.AttackerPopulated);
        _messageBroker.Publish(this, new SendInformationMessage(InvalidPlayerReserveMessage));
        base.Mission.EndMission();
    }

#if DEBUG
    internal RejoinSizingDebugState CaptureRejoinSizingState()
    {
        RejoinSizingDebugRecord heldSizing;
        RejoinSizingDebugRecord abort;
        RejoinSizingDebugError[] diagnosticErrors;
        lock (rejoinSizingDebugLock)
        {
            heldSizing = firstHeldSizing;
            abort = invalidOriginAbort;
            diagnosticErrors = rejoinSizingErrors.ToArray();
        }

        return new RejoinSizingDebugState(heldSizing, abort, diagnosticErrors);
    }

    private void TryRecordFirstHeldSizing(SideSizing sizing)
    {
        try
        {
            RejoinSizingDebugRecord captured = CaptureRejoinSizingRecord(
                "first-held-sizing-decision",
                sizing,
                willEndMission: false);
            bool retained = false;
            lock (rejoinSizingDebugLock)
            {
                if (firstHeldSizing == null)
                {
                    firstHeldSizing = captured;
                    retained = true;
                }
            }

            if (retained)
                LogRejoinSizingCapture(captured);
        }
        catch (Exception e)
        {
            RecordRejoinSizingDiagnosticError(
                "first-held-sizing-decision-capture-failed",
                e.GetType().Name + ": " + e.Message);
        }
    }

    private void TryRecordInvalidOriginAbort(SideSizing sizing)
    {
        try
        {
            RejoinSizingDebugRecord captured = CaptureRejoinSizingRecord(
                "immediately-before-invalid-origin-abort",
                sizing,
                willEndMission: true);
            bool retained = false;
            lock (rejoinSizingDebugLock)
            {
                if (invalidOriginAbort == null)
                {
                    invalidOriginAbort = captured;
                    retained = true;
                }
            }

            if (retained)
                LogRejoinSizingCapture(captured);
        }
        catch (Exception e)
        {
            RecordRejoinSizingDiagnosticError(
                "immediately-before-invalid-origin-abort-capture-failed",
                e.GetType().Name + ": " + e.Message);
        }
    }

    private RejoinSizingDebugRecord CaptureRejoinSizingRecord(
        string phase,
        SideSizing sizing,
        bool willEndMission)
    {
        Mission mission = Mission.Current;
        CoopBattleController controller = mission?.GetMissionBehavior<CoopBattleController>();
        string playerPartyId = GetLocalPlayerPartyId();
        RejoinPlayerAgentDebugState playerAgents = CaptureRejoinPlayerAgentState(mission, controller);
        RejoinDeploymentSetupDebugState deploymentSetup = CaptureRejoinDeploymentSetupState(mission);
        CoopTroopSupplierDebugObservation supplierObservation = CoopTroopSupplierRegistry.CaptureDebugObservation(
            controller?.Session.InstanceId,
            playerPartyId);
        return new RejoinSizingDebugRecord(
            phase,
            Process.GetCurrentProcess().Id,
            controller?.Session.InstanceId,
            controller?.Session.OwnControllerId,
            _playerSide,
            playerPartyId,
            sizing.DefenderPopulated,
            sizing.AttackerPopulated,
            sizing.DefenderOwned,
            sizing.AttackerOwned,
            sizing.BattleSize,
            sizing.HasAnyOwnedTroops,
            sizing.HasValidBattleSize,
            HasValidMissionSizing(sizing),
            HasLocalPlayerOrigin(),
            _heldSeconds,
            ReserveHoldDeadlineSeconds,
            _invalidBattleAbortRequested,
            willEndMission,
            playerAgents,
            deploymentSetup,
            supplierObservation,
            mission?.CurrentTime ?? -1f,
            DateTime.UtcNow,
            new RejoinResultTransitionDebugState(mission));
    }

    private static RejoinPlayerAgentDebugState CaptureRejoinPlayerAgentState(
        Mission mission,
        CoopBattleController controller)
    {
        Agent initialPlayerAgent = null;
        Agent mainAgent = null;
        string initialReadError = null;
        string mainReadError = null;
        try
        {
            initialPlayerAgent = mission?.InitialPlayerAgent;
        }
        catch (Exception e)
        {
            initialReadError = "initial-player-agent-read-failed: " + e.GetType().Name;
        }

        try
        {
            mainAgent = Agent.Main;
        }
        catch (Exception e)
        {
            mainReadError = "main-agent-read-failed: " + e.GetType().Name;
        }

        INetworkAgentRegistry registry = null;
        string registryError = null;
        try
        {
            if (!ContainerProvider.TryResolve<INetworkAgentRegistry>(out registry))
                registryError = "agent-registry-unavailable";
        }
        catch (Exception e)
        {
            registryError = "agent-registry-read-failed: " + e.GetType().Name;
        }

        RejoinMissionAgentDebugState initial = CaptureRejoinMissionAgentState(
            "InitialPlayerAgent", initialPlayerAgent, mission, registry, registryError, initialReadError);
        RejoinMissionAgentDebugState main = CaptureRejoinMissionAgentState(
            "MainAgent", mainAgent, mission, registry, registryError, mainReadError);
        bool? sameAgentInstance = initialPlayerAgent != null && mainAgent != null
            ? ReferenceEquals(initialPlayerAgent, mainAgent)
            : (bool?)null;
        string relationship = sameAgentInstance == true
            ? "same-agent"
            : sameAgentInstance == false
                ? "distinct-agents"
                : initialPlayerAgent == null && mainAgent == null
                    ? "initial-and-main-agent-unavailable"
                    : initialPlayerAgent == null
                        ? "initial-player-agent-unavailable"
                        : "main-agent-unavailable";
        return new RejoinPlayerAgentDebugState(
            controller?.Session.OwnControllerId,
            initial,
            main,
            sameAgentInstance,
            relationship,
            registryError);
    }

    private RejoinDeploymentSetupDebugState CaptureRejoinDeploymentSetupState(Mission mission)
    {
        try
        {
            DeploymentMissionController deploymentController =
                mission?.GetMissionBehavior<DeploymentMissionController>();
            if (deploymentController == null)
            {
                return new RejoinDeploymentSetupDebugState(
                    false,
                    null,
                    _sized,
                    "deployment-controller-unavailable",
                    mission == null ? "mission-unavailable" : "deployment-controller-unavailable");
            }

            bool teamSetupOver = deploymentController.TeamSetupOver;
            return new RejoinDeploymentSetupDebugState(
                true,
                teamSetupOver,
                _sized,
                teamSetupOver ? "setup-teams-returned" : "setup-teams-not-confirmed",
                null);
        }
        catch (Exception e)
        {
            return new RejoinDeploymentSetupDebugState(
                false,
                null,
                _sized,
                "deployment-state-unavailable",
                "deployment-state-read-failed: " + e.GetType().Name);
        }
    }

    private static RejoinMissionAgentDebugState CaptureRejoinMissionAgentState(
        string role,
        Agent agent,
        Mission mission,
        INetworkAgentRegistry registry,
        string registryError,
        string readError)
    {
        bool present = agent != null;
        bool active = false;
        bool inCurrentMission = false;
        int index = -1;
        string controller = null;
        bool registered = false;
        string networkAgentId = null;
        string currentAuthority = null;
        string originalOwner = null;
        string error = readError;
        if (!present)
        {
            error = AppendRejoinAgentDiagnosticError(error, "agent-unavailable");
        }
        else
        {
            try
            {
                active = agent.IsActive();
                inCurrentMission = agent.Mission == mission;
                index = agent.Index;
                controller = agent.Controller.ToString();
            }
            catch (Exception e)
            {
                error = AppendRejoinAgentDiagnosticError(
                    error, "agent-state-read-failed: " + e.GetType().Name);
            }
        }

        if (registry == null)
        {
            error = AppendRejoinAgentDiagnosticError(error, registryError);
        }
        else if (present)
        {
            try
            {
                if (registry.TryGetAgentInfo(agent, out CoopAgentInfo info))
                {
                    registered = true;
                    networkAgentId = info.AgentId.ToString("D");
                    currentAuthority = info.CurrentAuthority;
                    originalOwner = info.OriginalOwner;
                }
                else
                {
                    error = AppendRejoinAgentDiagnosticError(
                        error, "agent-registry-identity-unavailable");
                }
            }
            catch (Exception e)
            {
                error = AppendRejoinAgentDiagnosticError(
                    error, "agent-registry-identity-read-failed: " + e.GetType().Name);
            }
        }

        return new RejoinMissionAgentDebugState(
            role,
            present,
            active,
            inCurrentMission,
            index,
            controller,
            registry != null,
            registered,
            networkAgentId,
            currentAuthority,
            originalOwner,
            error);
    }

    private static string AppendRejoinAgentDiagnosticError(string current, string next)
    {
        if (string.IsNullOrWhiteSpace(next)) return current;
        return string.IsNullOrWhiteSpace(current) ? next : current + "; " + next;
    }

    private static void LogRejoinSizingCapture(RejoinSizingDebugRecord captured)
    {
        if (captured == null) return;

        try
        {
            Logger.Information(
                "[BattleSync] DEBUG rejoin sizing observation-json {ObservationJson}",
                JsonConvert.SerializeObject(captured));
        }
        catch (Exception e)
        {
            LogRejoinSizingDiagnosticFailure(
                e,
                "[BattleSync] Failed to emit rejoin sizing observation");
        }
    }

    private void RecordRejoinSizingDiagnosticError(string phase, string error)
    {
        try
        {
            Mission mission = Mission.Current;
            CoopBattleController controller = mission?.GetMissionBehavior<CoopBattleController>();
            var captured = new RejoinSizingDebugError(
                phase,
                Process.GetCurrentProcess().Id,
                controller?.Session.InstanceId,
                controller?.Session.OwnControllerId,
                mission?.CurrentTime ?? -1f,
                error,
                DateTime.UtcNow);
            bool retained = false;
            lock (rejoinSizingDebugLock)
            {
                if (rejoinSizingErrors.Count < 8)
                {
                    rejoinSizingErrors.Add(captured);
                    retained = true;
                }
            }

            if (retained)
                LogRejoinSizingDiagnosticError(captured);
        }
        catch (Exception e)
        {
            LogRejoinSizingDiagnosticFailure(
                e,
                "[BattleSync] Failed to record rejoin sizing diagnostic error");
        }
    }

    private static void LogRejoinSizingDiagnosticError(RejoinSizingDebugError captured)
    {
        try
        {
            Logger.Warning(
                "[BattleSync] DEBUG rejoin sizing diagnostic-error-json {ErrorJson}",
                JsonConvert.SerializeObject(captured));
        }
        catch (Exception e)
        {
            LogRejoinSizingDiagnosticFailure(
                e,
                "[BattleSync] Failed to emit rejoin sizing diagnostic error");
        }
    }

    private static void LogRejoinSizingDiagnosticFailure(Exception exception, string message)
    {
        try
        {
            Logger.Error(exception, message);
        }
        catch
        {
            // DEBUG observation must not change the held-sizing or abort decision.
        }
    }
#endif

    private bool HasLocalPlayerOrigin()
    {
        var supplier = _playerSide == BattleSideEnum.Attacker ? _attackerSupplier : _defenderSupplier;
        if (!supplier.WasPlayerHeroSupplied()
            && HasLocalPlayerOrigin(_playerSide, GetLocalPlayerPartyId(), _defenderSupplier, _attackerSupplier)) return true;
        var mission = base.Mission;
        return mission?.GetMissionBehavior<CoopBattleController>()?.HasRetainedPlayerAgent(mission.InitialPlayerAgent) == true;
    }

    internal bool HasSuppliedPlayerOrigin(BattleAgentSpawnData data)
    {
        if (data == null || data.Side != _playerSide || data.MapEventPartyId != GetLocalPlayerPartyId()) return false;
        var supplier = _playerSide == BattleSideEnum.Attacker ? _attackerSupplier : _defenderSupplier;
        return supplier.IsTroopAlreadySupplied(data.MapEventPartyId, data.CharacterId, data.TroopSeed);
    }

    private string GetLocalPlayerPartyId()
    {
        var playerSupplier = _playerSide == BattleSideEnum.Attacker ? _attackerSupplier : _defenderSupplier;
        return playerSupplier.PlayerPartyId;
    }

    internal static bool HasLocalPlayerOrigin(BattleSideEnum playerSide, string playerPartyId,
        CoopTroopSupplier defenderSupplier, CoopTroopSupplier attackerSupplier)
    {
        var playerSupplier = playerSide == BattleSideEnum.Attacker ? attackerSupplier : defenderSupplier;
        return playerSupplier.GetRemainingForParty(playerPartyId) > 0;
    }

    // This is the one point where an empty side becomes intentional rather than merely late. Record exactly
    // which reserve timed out so the controller can eventually release BattleEndLogic and the depletion patch
    // can call only that side depleted; the populated side must still field an agent.
    private static void AcceptMissingReserveSides(SideSizing sizing)
    {
        if (sizing.Ready) return;
        if (!sizing.DefenderPopulated)
            BattleSpawnGate.AcceptMissingReserveSide(BattleSideEnum.Defender);
        if (!sizing.AttackerPopulated)
            BattleSpawnGate.AcceptMissingReserveSide(BattleSideEnum.Attacker);
    }

    private static void LogSizingCompleted(SideSizing sizing)
    {
        if (sizing.Ready)
            Logger.Information("[BattleSync] Reserves landed after start; sized sides jointly: Defender={Def}, Attacker={Atk}", sizing.DefenderOwned, sizing.AttackerOwned);
        else
            Logger.Warning("[BattleSync] Reserves incomplete after {Sec}s hold (Def populated={DefP}, Atk populated={AtkP}) — sizing with what landed: Defender={Def}, Attacker={Atk}",
                ReserveHoldDeadlineSeconds, sizing.DefenderPopulated, sizing.AttackerPopulated, sizing.DefenderOwned, sizing.AttackerOwned);
    }

    // Snapshot both suppliers into a SideSizing. Read populated before owned so the pair can't tear: SetReserve
    // commits the entries then flips populated under one lock. Shared by AfterStart and OnMissionTick.
    private SideSizing ReadSizing()
    {
        bool defenderPopulated = _defenderSupplier.IsPopulated;
        bool attackerPopulated = _attackerSupplier.IsPopulated;
        // The SIDE's totals, not this client's share of them. The engine splits a fixed battle size in
        // proportion to the two numbers it is given, so a client sizing from what it happens to own measures
        // a side that is divided between players at a fraction of its strength: its opponent gets capped
        // against that fraction, and the divided side ends up fielding more men than the larger one.
        int defenderOwned = _defenderSupplier.SideTotalTroops;
        int attackerOwned = _attackerSupplier.SideTotalTroops;
        int battleSize = ResolveBattleSize(defenderPopulated, _defenderSupplier.BattleSize,
            attackerPopulated, _attackerSupplier.BattleSize);
        return new SideSizing(defenderPopulated, attackerPopulated, defenderOwned, attackerOwned, battleSize);
    }

    internal static int ResolveBattleSize(bool defenderPopulated, int defenderBattleSize,
        bool attackerPopulated, int attackerBattleSize)
    {
        if (defenderPopulated && attackerPopulated)
            return defenderBattleSize > 0 && defenderBattleSize == attackerBattleSize ? defenderBattleSize : 0;
        if (defenderPopulated)
            return Math.Max(0, defenderBattleSize);
        if (attackerPopulated)
            return Math.Max(0, attackerBattleSize);
        return 0;
    }

    // Re-run the engine's Init with the authoritative totals. Clear the placeholder phases first because
    // InitWithSinglePhase appends, and the sally-out controller may also have initialized phases before reserves land.
    private void RunJointInit(SideSizing sizing)
    {
        _missionAgentSpawnLogic._phases[(int)BattleSideEnum.Defender].Clear();
        _missionAgentSpawnLogic._phases[(int)BattleSideEnum.Attacker].Clear();
        _missionAgentSpawnLogic._numberOfTroopsInTotal[(int)BattleSideEnum.Defender] = 0;
        _missionAgentSpawnLogic._numberOfTroopsInTotal[(int)BattleSideEnum.Attacker] = 0;

        MissionSpawnSettings authoritativeSettings;
        int defenderTotal;
        int attackerTotal;
        int defenderInitial;
        int attackerInitial;
        if (_isSallyOut)
        {
            var targets = CalculateSallyOutSizing(
                sizing.DefenderOwned, sizing.AttackerOwned, sizing.BattleSize);
            defenderTotal = targets.DefenderTotal;
            attackerTotal = targets.AttackerTotal;
            defenderInitial = targets.DefenderInitial;
            attackerInitial = targets.AttackerInitial;
            authoritativeSettings = CreateSallyOutSpawnSettings();
        }
        else
        {
            var settings = CreateSandBoxBattleWaveSpawnSettings();
            var targets = ReinforcementFielder.RecoveryTargets.Calculate(
                sizing.DefenderOwned,
                sizing.AttackerOwned,
                sizing.BattleSize,
                settings.MaximumBattleSideRatio,
                settings.DefenderAdvantageFactor);
            defenderTotal = sizing.DefenderOwned;
            attackerTotal = sizing.AttackerOwned;
            defenderInitial = targets.Defenders;
            attackerInitial = targets.Attackers;
            authoritativeSettings = new MissionSpawnSettings(
                MissionSpawnSettings.InitialSpawnMethod.FreeAllocation,
                settings.ReinforcementTroopsTimingMethod,
                settings.ReinforcementTroopsSpawnMethod,
                settings.GlobalReinforcementInterval,
                settings.ReinforcementBatchPercentage,
                settings.DesiredReinforcementPercentage,
                settings.ReinforcementWavePercentage,
                settings.MaximumReinforcementWaveCount,
                settings.DefenderReinforcementBatchPercentage,
                settings.AttackerReinforcementBatchPercentage,
                settings.DefenderAdvantageFactor,
                settings.MaximumBattleSideRatio);
        }

        _missionAgentSpawnLogic.InitWithSinglePhase(defenderTotal, attackerTotal,
            defenderInitial, attackerInitial, spawnDefenders: true, spawnAttackers: true,
            in authoritativeSettings);
        if (_isSallyOut)
            _missionAgentSpawnLogic.SetCustomReinforcementSpawnTimer(
                new SallyOutReinforcementSpawnTimer(1f, 90f, 15f, 5));

        GuaranteePlayerInitialSlots();
        ClampPhasesToOwnedShare(BattleSideEnum.Defender, _defenderSupplier);
        ClampPhasesToOwnedShare(BattleSideEnum.Attacker, _attackerSupplier);

        // Init leaves both sides spawn-active; the native path clears them after Init but nothing does here, so
        // restore it — else SetupTeams's first side spawns both at once and the per-side freeze misses one.
        _missionAgentSpawnLogic.SetSpawnTroops(BattleSideEnum.Defender, spawnTroops: false);
        _missionAgentSpawnLogic.SetSpawnTroops(BattleSideEnum.Attacker, spawnTroops: false);
        var defenderSnapshot = _defenderSupplier.CaptureAllocationSnapshot();
        var attackerSnapshot = _attackerSupplier.CaptureAllocationSnapshot();
        _appliedAllocationRevision = MatchingAllocationRevision(defenderSnapshot.Revision, attackerSnapshot.Revision);
    }

    internal BattleSizeState CaptureBattleSizeState()
    {
        SideSizing sizing = ReadSizing();
        int defenderTarget;
        int attackerTarget;
        if (_isSallyOut)
        {
            var targets = CalculateSallyOutSizing(
                sizing.DefenderOwned, sizing.AttackerOwned, sizing.BattleSize);
            defenderTarget = targets.DefenderInitial;
            attackerTarget = targets.AttackerInitial;
        }
        else
        {
            var settings = CreateSandBoxBattleWaveSpawnSettings();
            var targets = ReinforcementFielder.RecoveryTargets.Calculate(
                sizing.DefenderOwned,
                sizing.AttackerOwned,
                sizing.BattleSize,
                settings.MaximumBattleSideRatio,
                settings.DefenderAdvantageFactor);
            defenderTarget = targets.Defenders;
            attackerTarget = targets.Attackers;
        }

        var defenderSnapshot = _defenderSupplier.CaptureAllocationSnapshot();
        var attackerSnapshot = _attackerSupplier.CaptureAllocationSnapshot();

        return new BattleSizeState(
            _sized,
            sizing.DefenderOwned,
            sizing.AttackerOwned,
            sizing.BattleSize,
            defenderTarget,
            attackerTarget,
            MatchingAllocationRevision(defenderSnapshot.Revision, attackerSnapshot.Revision));
    }

    // Reserve refreshes are sent as a reliable-ordered pair. Wait until both suppliers advanced, then resize
    // only the unspent lifetime quota; InitialSpawnNumber/InitialSpawnedNumber keep deployment one-shot.
    private void ReconcileRefreshedAllocation()
    {
        var defenderSnapshot = _defenderSupplier.CaptureAllocationSnapshot();
        var attackerSnapshot = _attackerSupplier.CaptureAllocationSnapshot();
        long allocationRevision = MatchingAllocationRevision(defenderSnapshot.Revision, attackerSnapshot.Revision);
        if (allocationRevision <= _appliedAllocationRevision
            || defenderSnapshot.BattleSize <= 0
            || defenderSnapshot.BattleSize != attackerSnapshot.BattleSize)
            return;

        BattleSpawnGate.RestoreReserveSide(BattleSideEnum.Defender);
        BattleSpawnGate.RestoreReserveSide(BattleSideEnum.Attacker);

        var settings = _missionAgentSpawnLogic.SpawnSettings;
        int defenderLifetimeTarget;
        int attackerLifetimeTarget;
        if (_isSallyOut)
        {
            var targets = CalculateSallyOutSizing(
                defenderSnapshot.SideTotalTroops,
                attackerSnapshot.SideTotalTroops,
                defenderSnapshot.BattleSize);
            defenderLifetimeTarget = targets.DefenderTotal;
            attackerLifetimeTarget = targets.AttackerTotal;
        }
        else
        {
            var targets = ReinforcementFielder.RecoveryTargets.Calculate(
                defenderSnapshot.SideTotalTroops,
                attackerSnapshot.SideTotalTroops,
                defenderSnapshot.BattleSize,
                settings.MaximumBattleSideRatio,
                settings.DefenderAdvantageFactor);
            defenderLifetimeTarget = CalculateLifetimeTarget(
                defenderSnapshot.SideTotalTroops,
                targets.Defenders,
                settings.ReinforcementWavePercentage,
                settings.MaximumReinforcementWaveCount);
            attackerLifetimeTarget = CalculateLifetimeTarget(
                attackerSnapshot.SideTotalTroops,
                targets.Attackers,
                settings.ReinforcementWavePercentage,
                settings.MaximumReinforcementWaveCount);
        }

        ReconcileSideLifetimeQuota(BattleSideEnum.Defender, defenderSnapshot, defenderLifetimeTarget);
        ReconcileSideLifetimeQuota(BattleSideEnum.Attacker, attackerSnapshot, attackerLifetimeTarget);

        _appliedAllocationRevision = allocationRevision;
        Logger.Information("[BattleSync] Reconciled refreshed native quotas: Defender={Def}, Attacker={Atk}",
            _missionAgentSpawnLogic.DefenderActivePhase.TotalSpawnNumber,
            _missionAgentSpawnLogic.AttackerActivePhase.TotalSpawnNumber);
    }

    internal static long MatchingAllocationRevision(long defenderRevision, long attackerRevision)
        => defenderRevision > 0 && defenderRevision == attackerRevision ? defenderRevision : 0;

    private void ReconcileSideLifetimeQuota(BattleSideEnum side,
        CoopTroopSupplier.AllocationSnapshot allocationSnapshot, int sideLifetimeTarget)
    {
        int ownedLifetimeTarget = allocationSnapshot.OwnedShareOf(sideLifetimeTarget);
        int reserved = _missionAgentSpawnLogic._battleSideSpawnContexts[(int)side].ReservedTroopsCount;
        ReconcilePhaseLifetimeQuota(
            _missionAgentSpawnLogic._phases[(int)side][0],
            ownedLifetimeTarget,
            allocationSnapshot.SuppliedTroops,
            reserved);
        _missionAgentSpawnLogic._numberOfTroopsInTotal[(int)side] = ownedLifetimeTarget;
    }

    internal static int CalculateLifetimeTarget(int sideTotal, int initialTarget, float wavePercentage,
        int maximumWaveCount)
    {
        initialTarget = Math.Min(Math.Max(0, initialTarget), Math.Max(0, sideTotal));
        int remaining = Math.Max(0, sideTotal - initialTarget);
        if (maximumWaveCount > 0)
        {
            int waveSize = Math.Max(1, (int)(initialTarget * wavePercentage));
            remaining = Math.Min(remaining, waveSize * maximumWaveCount);
        }
        return initialTarget + remaining;
    }

    internal static void ReconcilePhaseLifetimeQuota(MissionSpawnPhase phase, int refreshedOwnedTarget,
        int supplied, int reserved)
    {
        if (phase == null) return;

        int nativeSpawned = Math.Max(0, phase.TotalSpawnNumber - phase.RemainingSpawnNumber);
        int consumedSupply = Math.Max(0, supplied - reserved);
        int committed = Math.Max(nativeSpawned, consumedSupply);
        int remaining = Math.Max(0, refreshedOwnedTarget - committed);
        phase.RemainingSpawnNumber = remaining;
        phase.TotalSpawnNumber = committed + remaining;
    }

    private void GuaranteePlayerInitialSlots()
    {
        var defender = _missionAgentSpawnLogic.DefenderActivePhase;
        var attacker = _missionAgentSpawnLogic.AttackerActivePhase;
        AdjustInitialAllocations(
            defender.InitialSpawnNumber,
            attacker.InitialSpawnNumber,
            defender.TotalSpawnNumber,
            attacker.TotalSpawnNumber,
            _defenderSupplier.PlayerOwnedPartyCount,
            _attackerSupplier.PlayerOwnedPartyCount,
            out var defenderInitial,
            out var attackerInitial);
        defender.InitialSpawnNumber = defenderInitial;
        defender.RemainingSpawnNumber = defender.TotalSpawnNumber - defenderInitial;
        attacker.InitialSpawnNumber = attackerInitial;
        attacker.RemainingSpawnNumber = attacker.TotalSpawnNumber - attackerInitial;
    }

    internal static void AdjustInitialAllocations(
        int defenderInitial,
        int attackerInitial,
        int defenderTotal,
        int attackerTotal,
        int defenderPlayers,
        int attackerPlayers,
        out int adjustedDefenders,
        out int adjustedAttackers)
    {
        adjustedDefenders = defenderInitial;
        adjustedAttackers = attackerInitial;
        int defenderMinimum = Math.Min(defenderPlayers, defenderTotal);
        int attackerMinimum = Math.Min(attackerPlayers, attackerTotal);

        int transfer = Math.Min(Math.Max(0, defenderMinimum - adjustedDefenders),
            Math.Max(0, adjustedAttackers - attackerMinimum));
        adjustedDefenders += transfer;
        adjustedAttackers -= transfer;

        transfer = Math.Min(Math.Max(0, attackerMinimum - adjustedAttackers),
            Math.Max(0, adjustedDefenders - defenderMinimum));
        adjustedAttackers += transfer;
        adjustedDefenders -= transfer;
    }

    // Retained agents already consumed supply; only the unspent owner share needs new native origins.
    private void ClampPhasesToOwnedShare(BattleSideEnum side, CoopTroopSupplier supplier)
    {
        var allocation = supplier.CaptureAllocationSnapshot();
        foreach (var phase in _missionAgentSpawnLogic._phases[(int)side])
        {
            AdjustPhaseToOwnedShare(
                phase.TotalSpawnNumber,
                phase.InitialSpawnNumber,
                allocation.OwnedShareOf(phase.TotalSpawnNumber),
                allocation.OwnedShareOf(phase.InitialSpawnNumber),
                allocation.SuppliedTroops,
                out var total,
                out var initial,
                out var remaining);
            phase.TotalSpawnNumber = total;
            phase.InitialSpawnNumber = initial;
            phase.RemainingSpawnNumber = remaining;
        }
    }

    internal static void AdjustPhaseToOwnedShare(
        int sideTotal,
        int sideInitial,
        int ownedTotal,
        int ownedInitial,
        int supplied,
        out int total,
        out int initial,
        out int remaining)
    {
        supplied = Math.Max(0, supplied);
        total = Math.Max(0, ReachableSpawnNumber(sideTotal, ownedTotal) - supplied);
        initial = Math.Min(total, Math.Max(0, ReachableSpawnNumber(sideInitial, ownedInitial) - supplied));
        remaining = total - initial;
    }

    private static int ReachableSpawnNumber(int sideNumber, CoopTroopSupplier supplier)
        => ReachableSpawnNumber(sideNumber, supplier.OwnedShareOf(sideNumber));

    /// <summary>
    /// The largest spawn target this client can actually reach: never more than the side needs, and never more
    /// than the supplier will hand over when asked for that many.
    /// </summary>
    internal static int ReachableSpawnNumber(int sideNumber, int ownedShareOfSideNumber)
        => Math.Min(sideNumber, ownedShareOfSideNumber);

    internal static MissionSpawnSettings CreateSallyOutSpawnSettings()
        => SallyOutMissionController.CreateSallyOutSpawnSettings(0.01f, 0.1f);

    private SallyOutSizing CalculateSallyOutSizing(
        int defenderTotal, int attackerTotal, int battleSize)
    {
        if (Mission.GetMissionBehavior<SallyOutMissionController>() == null)
            return default;

        return CalculateSallyOutSizingFromBattleSize(defenderTotal, attackerTotal, battleSize);
    }

    internal static SallyOutSizing CalculateSallyOutSizingFromBattleSize(
        int defenderTotal, int attackerTotal, int battleSize)
    {
        const float defenderRatio = 0.25f;
        const float attackerRatio = 1f - defenderRatio;
        defenderTotal = Math.Min(defenderTotal, (int)(battleSize * defenderRatio));
        attackerTotal = Math.Min(attackerTotal, (int)(battleSize * attackerRatio));

        float attackerToDefenderRatio = attackerRatio / defenderRatio;
        if ((float)attackerTotal / defenderTotal <= attackerToDefenderRatio)
        {
            defenderTotal = Math.Min((int)(attackerTotal / attackerToDefenderRatio), defenderTotal);
        }
        else
        {
            attackerTotal = Math.Min((int)(defenderTotal * attackerToDefenderRatio), attackerTotal);
        }

        int initialPerSide = (int)Math.Ceiling((defenderTotal + attackerTotal) * 0.1f);
        return new SallyOutSizing(
            defenderTotal,
            attackerTotal,
            Math.Min(defenderTotal, initialPerSide),
            Math.Min(attackerTotal, initialPerSide));
    }

    // Zero phases so the first tick has active phases to read (else DefenderActivePhase NREs), without feeding Init
    // a 0/0 total. Clear first because the sally-out controller initializes its native phases before this handler.
    private void AddHeldPhases()
    {
        _missionAgentSpawnLogic._phases[(int)BattleSideEnum.Defender].Clear();
        _missionAgentSpawnLogic._phases[(int)BattleSideEnum.Attacker].Clear();
        _missionAgentSpawnLogic._phases[(int)BattleSideEnum.Defender].Add(new MissionSpawnPhase());
        _missionAgentSpawnLogic._phases[(int)BattleSideEnum.Attacker].Add(new MissionSpawnPhase());
    }

    internal readonly struct SallyOutSizing
    {
        public readonly int DefenderTotal;
        public readonly int AttackerTotal;
        public readonly int DefenderInitial;
        public readonly int AttackerInitial;

        public SallyOutSizing(int defenderTotal, int attackerTotal, int defenderInitial, int attackerInitial)
        {
            DefenderTotal = defenderTotal;
            AttackerTotal = attackerTotal;
            DefenderInitial = defenderInitial;
            AttackerInitial = attackerInitial;
        }
    }

    /// <summary>
    /// Snapshot of both suppliers plus the joint sizing derived from it (unit-testable — pure over its readings).
    /// Ready = both reserves landed; SizeNow additionally requires a positive combined total, so Init is never
    /// handed a 0/0 battle-size split.
    /// </summary>
    public readonly struct SideSizing
    {
        public readonly bool DefenderPopulated;
        public readonly bool AttackerPopulated;
        public readonly int DefenderOwned;
        public readonly int AttackerOwned;
        public readonly int BattleSize;

        public SideSizing(bool defenderPopulated, bool attackerPopulated, int defenderOwned, int attackerOwned,
            int battleSize)
        {
            DefenderPopulated = defenderPopulated;
            AttackerPopulated = attackerPopulated;
            DefenderOwned = defenderOwned;
            AttackerOwned = attackerOwned;
            BattleSize = battleSize;
        }

        // Both reserves landed: commit the joint sizing now (else keep holding both sides at zero).
        public bool Ready => DefenderPopulated && AttackerPopulated;

        // Ready and at least one side owns troops: run the real Init (a positive sum avoids Init's 0/0 NaN).
        public bool SizeNow => Ready && DefenderOwned + AttackerOwned > 0 && BattleSize > 0;

        public bool HasValidBattleSize => BattleSize > 0;

        /// <summary>Whether a timeout can safely degrade to a one-sided sizing instead of empty/empty.</summary>
        public bool HasAnyOwnedTroops => DefenderOwned + AttackerOwned > 0;
    }

    internal readonly struct BattleSizeState
    {
        public readonly bool IsSized;
        public readonly int DefenderTotal;
        public readonly int AttackerTotal;
        public readonly int BattleSize;
        public readonly int DefenderTarget;
        public readonly int AttackerTarget;
        public readonly long AllocationRevision;

        public BattleSizeState(
            bool isSized,
            int defenderTotal,
            int attackerTotal,
            int battleSize,
            int defenderTarget,
            int attackerTarget,
            long allocationRevision)
        {
            IsSized = isSized;
            DefenderTotal = defenderTotal;
            AttackerTotal = attackerTotal;
            BattleSize = battleSize;
            DefenderTarget = defenderTarget;
            AttackerTarget = attackerTarget;
            AllocationRevision = allocationRevision;
        }
    }
}
