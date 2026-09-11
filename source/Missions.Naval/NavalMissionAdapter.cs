#if DEBUG
using Common.Logging;
using HarmonyLib;
using NavalDLC;
using TaleWorlds.MountAndBlade.Source.Missions;
using SandBox.Missions.MissionLogics;
using Missions.Battles;
using NavalDLC.Missions;
using NavalDLC.Missions.MissionLogics;
using NavalDLC.Missions.Objects;
using NavalDLC.Missions.Objects.UsableMachines;
using NavalDLC.Missions.ShipControl;
using NavalDLC.Missions.ShipActuators;
using NavalDLC.Missions.ShipInput;
using System;
using System.Linq;
using System.Collections.Generic;
using Serilog;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.Engine;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
using TaleWorlds.ObjectSystem;

namespace Missions.Naval;

public sealed class NavalMissionAdapter : INavalMissionAdapter, INavalNativeMissionAdapter, INavalHelmReplicationAdapter, INavalDriftAdapter, INavalPresentationAdapter
{
    private NavalLabBehavior behavior;
    private readonly Harmony harmony = new Harmony("coop.warsails.lab.physics");
    public string Blocker => behavior?.Blocker;
    public object StartupDiagnostics => behavior?.StartupDiagnostics;
    public Agent[] Agents => behavior?.Agents ?? Array.Empty<Agent>();

    public void Preflight()
    {
        if (NavalLabPhysicsPatches.Active?.RequiresProcessExit == true)
            throw new InvalidOperationException("Held helm fixtures require process exit before another naval fixture can open.");
        if (Mission.Current != null) throw new InvalidOperationException("Leave the current mission before starting the naval lab.");
        if (MBObjectManager.Instance.GetObject<ShipHull>(NavalLabManifest.HullId) == null
            || CharacterObject.Find("imperial_infantryman") == null)
            throw new InvalidOperationException("Installed lab hull or troop definition is unavailable.");
    }

    public Mission Open(NavalLabManifest manifest, MissionBehavior controller, string ownControllerId)
    {
        Preflight();
        if (Mission.Current != null) throw new InvalidOperationException("Leave the current mission before starting the naval lab.");
        var hull = MBObjectManager.Instance.GetObject<ShipHull>(NavalLabManifest.HullId);
        var troop = CharacterObject.Find("imperial_infantryman");
        if (hull == null || troop == null) throw new InvalidOperationException("Installed lab hull or troop definition is unavailable.");
        behavior = new NavalLabBehavior(manifest, ownControllerId, hull, troop);
        if (manifest.Mode == NavalLabMode.SingleClientNative)
            behavior.NativeAuthority = () => controller is NavalLabController lab && lab.HasSingleClientAuthority;
        NavalLabPhysicsPatches.Active = behavior;
        harmony.PatchAll(typeof(NavalMissionAdapter).Assembly);
        var rec = new MissionInitializerRecord(NavalLabManifest.SceneId)
        {
            PlayingInCampaignMode = false,
            NeedsRandomTerrain = false
        };
        rec.AtmosphereOnCampaign.NauticalInfo.UsesNavalSimulatedWater = 1;
        return NavalMissionState.OpenNew("NavalLab", rec, mission =>
        {
            var behaviors = new List<MissionBehavior>
            {
                new NavalShipsLogic(), new NavalAgentsLogic(), new WaveParametersComputerLogic(),
                new AgentHumanAILogic(), new MissionOptionsComponent(), behavior, controller
            };
            if (manifest.Mode == NavalLabMode.SingleClientNative || manifest.Mode == NavalLabMode.TwoClientNative)
            {
                behaviors.Add(new NavalLabBattlePowerCalculationLogic(behavior));
                behaviors.Add(new NavalTrajectoryPlanningLogic());
                behaviors.AddRange(behavior.CreateNativeViews(mission));
            }
            return behaviors;
        });
    }

    public void SetAuthority(bool simulate) => behavior?.SetAuthority(simulate);
    public void MaterializeFactoryProbe(bool electedHost, Func<bool> authorityValid) =>
        behavior.MaterializeFactoryProbe(electedHost, authorityValid);
    public string CompleteDeployment() => behavior?.CompleteNativeDeployment() ?? "rejected:unavailable";
    public void Hold() => behavior?.Hold();
    public MatrixFrame[] ReadFrames() => behavior?.Ships.Where(ship => ship != null).Select(ship => ship.GlobalFrame).ToArray() ?? Array.Empty<MatrixFrame>();
    public bool ApplyFrames(MatrixFrame[] frames)
    {
        if (behavior == null || behavior.Blocker != null || behavior.IsSingleClientNative || behavior.Simulating || frames.Length != behavior.Ships.Length) return false;
        if (behavior.IsFactoryProbe && !behavior.CanApplyFactoryFrames()) return false;
        for (int i = 0; i < frames.Length; i++)
        {
            if (behavior.IsTwoClientNative && !behavior.CanApplyFactoryFrames()) return false;
            var entity = behavior.Ships[i].GameEntity;
            entity.SetGlobalFrame(frames[i], isTeleportation: !behavior.IsTwoClientNative);
            entity.UpdateAttachedNavigationMeshFaces();
        }
        if (behavior.IsFactoryProbe && !behavior.CanApplyFactoryFrames()) return false;
        if (behavior.IsTwoClientNative) behavior.RefreshFollowerStationTargets();
        return true;
    }
    public void SetHelm(int ship, float rudder, bool row)
    {
        if (behavior == null || behavior.HasNativeViews || !behavior.Simulating || ship < 0 || ship >= behavior.Ships.Length) return;
        var input = ShipInputRecord.None();
        input.SetRudderLateral(rudder);
        input.SetRowerLongitudinal(row ? RowerLongitudinalInput.Forward : RowerLongitudinalInput.Stop);
        ((PlayerShipController)behavior.Ships[ship].Controller).SetInput(in input);
    }
    public string SetHeldHelm(int ship, bool take) => behavior?.SetHeldHelm(ship, take) ?? "rejected:unavailable";
    public string StartAgentControl(string kind, int ship, float value) =>
        behavior?.StartAgentControl(kind, ship, value) ?? "rejected:unavailable";
    public void TickAgentControl(float dt) => behavior?.TickAgentControl(dt);
    public void CancelControls() => behavior?.CancelControls();
    public object Inspect() => behavior?.Inspect();
    public void ConfigureNative(Func<bool> authority, Action<Missions.Messages.NetworkNavalLabHelmInput> sendInput)
    { behavior.NativeAuthority = authority; behavior.SendNativeInput = sendInput; }
    public Missions.Messages.NetworkNavalLabStations CreateStations() => behavior.CreateStations();
    public void ApplyStations(Missions.Messages.NetworkNavalLabStations stations) => behavior.ApplyStations(stations);
    public bool ObserveStations(Missions.Messages.NetworkNavalLabStations stations) => behavior.ObserveStations(stations);
    public void ConfigureHelmReplication(Action<Missions.Messages.NetworkNavalLabHelmOccupancy> send, Action<Agent> forgetMovement)
    { behavior.SendHelmOccupancy = send; behavior.ForgetHelmMovement = forgetMovement; }
    public void ApplyHelmOccupancy(Missions.Messages.NetworkNavalLabHelmOccupancy value) => behavior.ApplyHelmOccupancy(value);
    public long HelmMovementRevision(Guid combatantId, Agent agent) => behavior.HelmMovementRevision(combatantId, agent);
    public bool IsCommittedOarMovement(Guid incarnationId, Guid combatantId, Agent agent) =>
        behavior?.IsCommittedOarMovement(incarnationId, combatantId, agent) == true;
    public bool IsOccupiedHelmMovement(Guid incarnationId, Guid combatantId, Agent agent) =>
        behavior?.IsOccupiedHelmMovement(incarnationId, combatantId, agent) == true;
    public void ApplyNativeInput(Missions.Messages.NetworkNavalLabHelmInput input) => behavior.ApplyNativeInput(input);
    public void NeutralizeNativeInput(int ship) => behavior.NeutralizeNativeInput(ship);
    public Missions.Messages.NetworkNavalLabSailState[] ReadSailStates() => behavior.ReadSailStates();
    public void ApplySailFeedback(Missions.Messages.NetworkNavalLabFrames frames) => behavior.ApplySailFeedback(frames);
    public void ClearSailFeedback() => behavior.ClearSailFeedback();
    public string RequestAxesPulse(Guid operationId, int ship, float lateral, bool row, long deadlineUtcTicks)
        => behavior?.RequestAxesPulse(operationId, ship, lateral, row, deadlineUtcTicks) ?? "rejected:no_fixture";
    public object StartDrift(Guid operationId, int seconds) => behavior?.StartDrift(operationId, seconds) ?? new { unavailable = "no_fixture" };
    public object InspectDrift() => behavior?.InspectDrift() ?? new { unavailable = "no_fixture" };
    public object InspectControlStatus() => behavior?.InspectControlStatus() ?? new { unavailable = "no_fixture" };
    public object InspectSailStatus() => behavior?.InspectSailStatus() ?? new { unavailable = "no_fixture" };
    public string RequestSail(int state) => behavior?.RequestSail(state) ?? "rejected:no_fixture";
    public string RequestNativeHelm(Guid operationId, int ship, bool take) =>
        behavior?.RequestNativeHelm(operationId, ship, take) ?? "rejected:no_fixture";
    public object InspectHelmStatus() => behavior?.InspectHelmStatus() ?? new { unavailable = "no_fixture" };
    public Missions.Messages.NetworkNavalLabPresentation[] CapturePresentation(long sequence) => behavior.CapturePresentation(sequence);
    public bool ValidatePresentation(Missions.Messages.NetworkNavalLabFrames frames) => behavior.ValidatePresentation(frames);
    public void AcceptPresentation(Missions.Messages.NetworkNavalLabFrames frames) => behavior.AcceptPresentation(frames);
    public void TickPresentation(float dt) => behavior.TickPresentation(dt);
    public void ClearPresentation() => behavior.ClearPresentation();
    public object InspectPresentationStatus() => behavior.InspectPresentationStatus();
    public string RequestPresentationPulse(Guid operationId, int ship, float lateral, string kind, long deadlineUtcTicks)
        => behavior.RequestPresentationPulse(operationId, ship, lateral, kind, deadlineUtcTicks);
    public void Dispose()
    {
        if (behavior?.IsSingleClientNative == true || behavior?.IsFactoryProbe == true) behavior.Hold();
        behavior?.StopActivationDiagnostics();
        behavior?.CancelControls();
        // Held fixtures retain their exact capture and damage guards until process exit, including after behavior removal.
        if (NavalLabPhysicsPatches.Active?.RequiresProcessExit == true)
        {
            behavior = null;
            return;
        }
        NavalLabPhysicsPatches.Active = null;
        harmony.UnpatchAll(harmony.Id);
        behavior = null;
    }
}

internal sealed partial class NavalLabBehavior : MissionLogic
{
    private static readonly ILogger Logger = LogManager.GetLogger<NavalLabBehavior>();
    private readonly NavalLabManifest manifest;
    private readonly string ownControllerId;
    private readonly ShipHull hull;
    private readonly BasicCharacterObject troop;
    public MissionShip[] Ships { get; private set; } = Array.Empty<MissionShip>();
    public Agent[] Agents { get; private set; } = Array.Empty<Agent>();
    public bool Simulating { get; private set; }
    internal bool IsHeldHelm => manifest.Mode == NavalLabMode.HeldHelm;
    internal bool IsSingleClientNative => manifest.Mode == NavalLabMode.SingleClientNative;
    internal bool IsTwoClientNative => manifest.Mode == NavalLabMode.TwoClientNative;
    internal bool HasNativeViews => IsSingleClientNative || IsTwoClientNative;
    internal bool IsFactoryProbe => manifest.Mode == NavalLabMode.FactoryAuthorityProbe || IsTwoClientNative;
    internal bool RequiresProcessExit => IsHeldHelm || IsSingleClientNative || IsFactoryProbe;
    public object StartupDiagnostics { get; private set; }
    private readonly bool[] helmLifecycleInitialized = new bool[2];
    private Agent heldHelmAgent;
    private StandingPoint heldHelmPoint;
    private double heldHelmDeadline;
    private string heldHelmStatus = "not_taken";
    private int helmUseCallbacks;
    private int helmStopCallbacks;
    private int helmTakeCalls;
    private int helmReleaseCalls;
    internal const int HelmTraceLimit = 32;
    internal const int HelmTraceStackLimit = 8;
    private readonly List<HelmTraceRecord> helmTrace = new List<HelmTraceRecord>();

    internal sealed class HelmTraceRecord
    {
        public int Sequence { get; internal set; }
        public int? EntrySequence { get; internal set; }
        public string Phase { get; internal set; }
        public int ThreadId { get; internal set; }
        public long Timestamp { get; internal set; }
        public string AgentUsedObject { get; internal set; }
        public string PointUser { get; internal set; }
        public bool LeaseMatches { get; internal set; }
        public string ExceptionType { get; internal set; }
        public string NativeState { get; } = "unavailable:native_lifetime_not_proven;managed_identities_only";
        public string[] Stack { get; internal set; } = Array.Empty<string>();
    }

    internal sealed class HelmTraceCall
    {
        internal NavalLabBehavior Behavior;
        internal Agent Agent;
        internal UsableMissionObject Point;
        internal string Operation;
        internal int EntrySequence;
    }

    internal HelmTraceRecord[] HelmTrace
    {
        get { lock (helmTrace) return helmTrace.ToArray(); }
    }

    private bool IsHelmTracePair(Agent agent, UsableMissionObject point)
    {
        if (!IsHeldHelm || agent == null || point == null) return false;
        int slot = Array.IndexOf(manifest.Controllers, ownControllerId);
        return slot >= 0 && slot < Ships.Length && slot * NavalLabManifest.CrewPerShip < Agents.Length
            && Agents[slot * NavalLabManifest.CrewPerShip] == agent
            && Ships[slot]?.ShipControllerMachine?.PilotStandingPoint == point;
    }

    internal HelmTraceCall BeginHelmTrace(Agent agent, UsableMissionObject point, string operation)
    {
        try
        {
            if (!IsHelmTracePair(agent, point)) return null;
            int sequence = RecordHelmTrace(agent, point, operation + ":entry", stack: true);
            return sequence == 0 ? null : new HelmTraceCall
            {
                Behavior = this, Agent = agent, Point = point, Operation = operation, EntrySequence = sequence
            };
        }
        catch { return null; }
    }

    internal static void EndHelmTrace(HelmTraceCall call, Exception exception)
    {
        if (call == null) return;
        // Keep the entry pair because StopUsingGameObjectAux clears the agent's used object before returning.
        call.Behavior.RecordHelmTrace(call.Agent, call.Point, call.Operation + ":exit", call.EntrySequence, exception);
    }

    private int RecordHelmTrace(Agent agent, UsableMissionObject point, string phase,
        int? entrySequence = null, Exception exception = null, bool stack = false)
    {
        try
        {
            lock (helmTrace)
            {
                if (helmTrace.Count >= HelmTraceLimit || !IsHelmTracePair(agent, point)) return 0;
                var record = new HelmTraceRecord
                {
                    Sequence = helmTrace.Count + 1, EntrySequence = entrySequence, Phase = phase,
                    ThreadId = System.Threading.Thread.CurrentThread.ManagedThreadId,
                    Timestamp = System.Diagnostics.Stopwatch.GetTimestamp(),
                    // Both getters read managed fields; never query action/controller pointers from these hooks.
                    AgentUsedObject = agent.CurrentlyUsedGameObject == null ? "none" : agent.CurrentlyUsedGameObject == point ? "exact" : "other",
                    PointUser = point.UserAgent == null ? "none" : point.UserAgent == agent ? "exact" : "other",
                    LeaseMatches = heldHelmAgent == agent && heldHelmPoint == point,
                    ExceptionType = exception?.GetType().FullName
                };
                if (stack)
                {
                    var frames = new List<string>();
                    for (int i = 0; i < HelmTraceStackLimit; i++)
                    {
                        var method = new System.Diagnostics.StackFrame(i + 2, false).GetMethod();
                        if (method == null) break;
                        string name = method.DeclaringType?.FullName + "." + method.Name;
                        frames.Add(name.Length > 180 ? name.Substring(0, 180) : name);
                    }
                    record.Stack = frames.ToArray();
                }
                helmTrace.Add(record);
                return record.Sequence;
            }
        }
        catch { return 0; }
    }
    private Agent controlledAgent;
    private string controlKind;
    private int controlShip;
    private float controlValue;
    private double controlDeadline;
    private Vec2 previousInput;
    private Vec2 writtenInput;
    private bool jumpWritten;
    private bool wasWalking;
    private static double ControlNow => (double)System.Diagnostics.Stopwatch.GetTimestamp() / System.Diagnostics.Stopwatch.Frequency;
    private string startupPhase = "not_started";
    internal const int ActivationTransitionLimit = 32;
    private readonly List<ActivationTransition> activationTransitions = new List<ActivationTransition>();
    private long activationCallbackOrdinal;
    private bool activationDiagnosticsStopped;
    internal ActivationTransition[] ActivationTransitions => activationTransitions.ToArray();
    public long ForceApplications;
    public long FixedTicks;
    public long ActiveFixedTicks;
    private string blocker;
    public string Blocker => System.Threading.Volatile.Read(ref blocker);

    public NavalLabBehavior(NavalLabManifest manifest, string ownControllerId, ShipHull hull, BasicCharacterObject troop)
    {
        this.manifest = manifest;
        this.ownControllerId = ownControllerId;
        this.hull = hull;
        this.troop = troop;
    }

    public override void OnBehaviorInitialize()
    {
        Mission.MissionTeamAIType = Mission.MissionTeamAITypeEnum.NavalBattle;
        Mission.DisableDying = true;
        // DefaultNavalMissionLogic normally provides this before ships initialize their sails.
        if (!SailWindProfile.IsSailWindProfileInitialized)
            SailWindProfile.InitializeProfile();
        if (HasNativeViews) InitializeNativeTeams();
    }

    public override void OnMissionStateFinalized()
    {
        if (HasNativeViews) nativeTerminalHold = true;
        StopActivationDiagnostics();
        SailWindProfile.FinalizeProfile();
    }

    public override void AfterStart()
    {
        activationCallbackOrdinal++;
        try
        {
            RecordStartup("scene_loaded");
            if (IsFactoryProbe) return;
            InitializeFixture();
            RecordStartup("fixture_initialized");
        }
        catch (Exception exception)
        {
            // Preserve the first failure before any later script tick can obscure it.
            Logger.Error(exception, "[NavalLabStartup] {Incarnation} failed during {Phase}", manifest.IncarnationId, startupPhase);
            Reject(exception.ToString());
            RecordStartup("fixture_failed");
        }
    }

    private void InitializeFixture()
    {
        if (Mission.MissionBehaviors.Any(item => item is CampaignMissionComponent || item is CoopBattleController))
            throw new InvalidOperationException("Campaign behavior attached to the synthetic lab.");
        var teams = HasNativeViews ? nativeTeams : new[]
        {
            Mission.Teams.Add(BattleSideEnum.Attacker),
            Mission.Teams.Add(BattleSideEnum.Defender)
        };
        Mission.PlayerTeam = teams[Array.IndexOf(manifest.Controllers, ownControllerId)];
        teams[0].SetIsEnemyOf(teams[1], false);
        teams[1].SetIsEnemyOf(teams[0], false);
        var shipsLogic = Mission.GetMissionBehavior<NavalShipsLogic>();
        var agentsLogic = Mission.GetMissionBehavior<NavalAgentsLogic>();
        agentsLogic.UpdateTeamAgentsData();
        Ships = new MissionShip[manifest.Ships.Length];
        Agents = new Agent[manifest.Combatants.Length];
        for (int i = 0; i < Ships.Length; i++)
        {
            var frame = MatrixFrame.Identity;
            frame.origin = new Vec3(250f + (i * 60f), 250f, 0f);
            if (IsFactoryProbe) BeginFactoryHull(i);
            RecordStartup("factory_begin:" + i);
            Ships[i] = shipsLogic.SpawnShip(new NavalLabShipOrigin(hull, Reject),
                in frame, teams[i], spawnAnchored: IsSingleClientNative || IsFactoryProbe, checkForFreeArea: false);
            if (IsFactoryProbe) FinishFactoryHull(i);
            RecordStartup("factory_complete:" + i);
            if (!Ships[i].IsInitialized || Ships[i]._actuators == null || Ships[i].Physics == null
                || !Ships[i].Physics.IsInitialized || Ships[i].Formation == null)
                throw new InvalidOperationException("Factory returned an incomplete native ship at slot " + i);
            if (!IsSingleClientNative && !IsFactoryProbe) DisableStartupBody(i);
            else if (IsSingleClientNative) RecordActivationTransition(i, "factory_active_no_disable");
            Ships[i].SetCanBeTakenOver(false);
            Ships[i].SetController(HasNativeViews ? ShipControllerType.None : ShipControllerType.Player, autoUpdateController: false);
            Ships[i].SetAnchor(IsSingleClientNative || IsFactoryProbe);
            if (Ships[i].InnerDeckLocalFrames.Count < NavalLabManifest.CrewPerShip)
                throw new InvalidOperationException("Hull lacks the five required enumerated deck spawn frames.");
            for (int j = 0; j < NavalLabManifest.CrewPerShip; j++)
            {
                var index = (i * NavalLabManifest.CrewPerShip) + j;
                var spawn = Ships[i].GlobalFrame.TransformToParent(Ships[i].InnerDeckLocalFrames[j]);
                bool owned = ownControllerId == manifest.Controllers[i];
                var origin = new NavalLabAgentOrigin(troop, index + 1, owned, Reject);
                var agent = Mission.SpawnAgent(new AgentBuildData(troop)
                    .Team(teams[i]).TroopOrigin(origin).Equipment(new Equipment())
                    .InitialPosition(spawn.origin).InitialDirection(spawn.rotation.f.AsVec2)
                    .Controller(owned ? (j == 0 ? AgentControllerType.Player : AgentControllerType.AI) : AgentControllerType.None));
                agent.SetMortalityState(Agent.MortalityState.Invulnerable);
                agent.GetComponent<AgentNavalComponent>().SetCanDrown(false);
                agent.GetComponent<AgentNavalComponent>().SetCanBurn(false);
                agent.Formation = Ships[i].Formation;
                agentsLogic.AddAgentToShip(agent, Ships[i]);
                Agents[index] = agent;
                if (owned && j == 0) Mission.MainAgent = agent;
            }
        }
        if (HasNativeViews)
        {
            PrepareNativeDeployment();
            return;
        }
        Mission.IsDeploymentFinished = true;
        Mission.AllowAiTicking = true;
        shipsLogic.SetDeploymentMode(false);
        agentsLogic.SetDeploymentMode(false);
        agentsLogic.SetSpawnReinforcementsOnTick(false);
        if (manifest.Mode == NavalLabMode.HeldHelm)
        {
            int slot = Array.IndexOf(manifest.Controllers, ownControllerId);
            var machine = Ships[slot].ShipControllerMachine;
            if (machine?.PilotStandingPoint == null || machine.AttachedShip != Ships[slot]
                || Ships[slot].Formation.Team != Mission.PlayerTeam)
                throw new InvalidOperationException("Owned native helm standing point is unavailable.");
            // Only the owned helm needs cleanup components and naval logic references, not ship-wide deployment.
            InitializeHelmLifecycle(slot);
        }
    }

    internal void InitializeHelmLifecycle(int slot)
    {
        if (manifest.Mode != NavalLabMode.HeldHelm || slot < 0 || slot >= Ships.Length
            || manifest.Controllers[slot] != ownControllerId || helmLifecycleInitialized[slot]) return;
        Ships[slot].ShipControllerMachine.OnDeploymentFinished();
        helmLifecycleInitialized[slot] = true;
    }

    internal void RecordStartup(string phase)
    {
        startupPhase = phase;
        bool sailWindProfileInitialized = SailWindProfile.IsSailWindProfileInitialized;
        Logger.Information("[NavalLabStartup] {Incarnation} {Phase}", manifest.IncarnationId, phase);
        try
        {
            var entities = new List<GameEntity>();
            Mission.Scene.GetAllEntitiesWithScriptComponent<MissionShip>(ref entities);
            var ships = entities.Where(entity => entity != null && entity.WeakEntity.IsValid)
                .Select(entity => entity.GetFirstScriptOfType<MissionShip>())
                .Where(ship => ship != null).Select(InspectShipInventory).ToArray();
            // The native name-based query can be empty even after the factory returns live hulls.
            StartupDiagnostics = new
            {
                phase, blocker = Blocker, sailWindProfileInitialized, sceneShips = ships,
                registeredShips = Mission.MissionObjects.OfType<MissionShip>().Select(InspectShipInventory).ToArray(),
                fixtureShips = Ships.Select(InspectShipInventory).ToArray()
            };
            Logger.Information("[NavalLabStartup] {Incarnation} inventory {@Diagnostics}", manifest.IncarnationId, StartupDiagnostics);
        }
        catch (Exception exception)
        {
            // Diagnostic enumeration must not replace the original factory exception.
            StartupDiagnostics = new { phase, blocker = Blocker, sailWindProfileInitialized, inventoryFailure = exception.ToString() };
            Logger.Error(exception, "[NavalLabStartup] {Incarnation} inventory failed", manifest.IncarnationId);
        }
    }

    internal void RecordInitializationFailure(Exception exception)
    {
        Logger.Error(exception, "[NavalLabStartup] {Incarnation} InitForMission failed", manifest.IncarnationId);
        Reject(exception.ToString());
        RecordStartup("init_for_mission_failed");
    }

    public void SetAuthority(bool simulate)
    {
        activationCallbackOrdinal++;
        if (IsFactoryProbe) { SetFactoryProbeAuthority(simulate); return; }
        if (IsSingleClientNative)
        {
            if (nativeDeploymentComplete && (!simulate || !CanUseNativeControls)) Hold();
            return;
        }
        simulate &= Blocker == null && manifest.Mode == NavalLabMode.Activation;
        if (!simulate)
        {
            if (Simulating) HoldBodies();
            return;
        }
        if (Ships.Length != manifest.Ships.Length || Ships.Any(ship => ship == null || !ship.GameEntity.IsValid))
        {
            Reject("ship.activation_unconfirmed:incomplete_hulls");
            RecordActivationRollback();
            HoldBodies(recordActivationRollback: true);
            return;
        }
        if (!Simulating)
            for (int i = 0; i < Ships.Length; i++)
            {
                RecordActivationTransition(i, "host_enable_before");
                Ships[i].GameEntity.EnableDynamicBody();
                RecordActivationTransition(i, "host_enable_after");
            }

        // The native enable call alone is not evidence that force integration is active.
        int inactive = Array.FindIndex(Ships, ship => !ship.GameEntity.HasDynamicRigidBodyAndActiveSimulation());
        if (inactive >= 0)
        {
            Reject("ship.activation_unconfirmed:slot_" + inactive);
            RecordActivationRollback();
            HoldBodies(recordActivationRollback: true);
            return;
        }
        Simulating = true;
    }

    public void Hold()
    {
        ClearPresentation();
        if (IsFactoryProbe) { HoldFactoryProbe(); return; }
        if (IsSingleClientNative)
        {
            if (nativeTerminalHold) return;
            nativeTerminalHold = true;
            if (Mission != null) Mission.AllowAiTicking = false;
            try { CancelControls(); CancelNativeControls(); }
            catch (Exception exception) { Reject("controls.cleanup_failed:" + exception.GetType().FullName); }
        }
        HoldBodies();
    }

    private void HoldBodies(bool recordActivationRollback = false)
    {
        Simulating = false;
        foreach (var ship in Ships.Where(ship => ship != null && ship.GameEntity.IsValid))
        {
            ship.GameEntity.DisableDynamicBodySimulation();
            if (recordActivationRollback) RecordActivationTransition(Array.IndexOf(Ships, ship), "rollback_disable_after");
        }
    }

    internal void DisableStartupBody(int slot)
    {
        RecordActivationTransition(slot, "startup_disable_before");
        Ships[slot].GameEntity.DisableDynamicBodySimulation();
        RecordActivationTransition(slot, "startup_disable_after");
    }

    internal void StopActivationDiagnostics() => activationDiagnosticsStopped = true;

    private void RecordActivationRollback()
    {
        for (int i = 0; i < manifest.Ships.Length; i++)
            RecordActivationTransition(i, "rollback_before");
    }

    internal void RecordActivationTransition(int slot, string phase)
    {
        // Stop before any native query, including after teardown or when retention is full.
        if (activationDiagnosticsStopped || activationTransitions.Count >= ActivationTransitionLimit) return;
        var row = new ActivationTransition
        {
            Sequence = activationTransitions.Count + 1, Slot = slot, Phase = phase,
            CallbackOrdinal = activationCallbackOrdinal,
            FixedTicks = System.Threading.Interlocked.Read(ref FixedTicks)
        };
        try
        {
            var ship = slot >= 0 && slot < Ships.Length ? Ships[slot] : null;
            if (ship == null) row.Unavailable = "ship_not_created";
            else
            {
                var entity = ship.GameEntity;
                row.NativePointer = entity.Pointer.ToUInt64();
                if (!entity.IsValid) row.Unavailable = "invalid_ship_entity";
                else if (!ship.IsInitialized || ship._actuators == null || ship.Physics?.IsInitialized != true || ship.Formation == null)
                    row.Unavailable = "incomplete_ship";
                else
                {
                    row.DynamicBody = ActivationRead<bool>.Read(() => entity.HasDynamicRigidBody());
                    if (row.DynamicBody.Value == true)
                    {
                        row.ActiveSimulation = ActivationRead<bool>.Read(() => entity.HasDynamicRigidBodyAndActiveSimulation());
                        row.BodyFlag = ActivationRead<uint>.Read(() => (uint)entity.BodyFlag);
                        row.PhysicsState = ActivationRead<bool>.Read(() => entity.GetPhysicsState());
                    }
                    else row.Unavailable = row.DynamicBody.Value == false ? "dynamic_body_missing" : "dynamic_body_read_error";
                }
            }
        }
        catch (Exception exception) { row.Error = exception.GetType().FullName; }
        activationTransitions.Add(row);
        try { Logger.Information("[NavalLabActivation] {Incarnation} {@Transition}", manifest.IncarnationId, row); }
        catch (Exception) { /* Logging must not prevent the following native mutation or rollback. */ }
    }

    internal sealed class ActivationRead<T> where T : struct
    {
        public T? Value { get; private set; }
        public string Unavailable { get; private set; }
        public string Error { get; private set; }

        public ActivationRead(string unavailable) { Unavailable = unavailable; }

        internal static ActivationRead<T> Read(Func<T> getter)
        {
            var result = new ActivationRead<T>(null);
            try { result.Value = getter(); }
            catch (Exception exception) { result.Error = exception.GetType().FullName; }
            return result;
        }
    }

    internal sealed class ActivationTransition
    {
        public int Sequence { get; internal set; }
        public int Slot { get; internal set; }
        public ulong? NativePointer { get; internal set; }
        public string Phase { get; internal set; }
        public long CallbackOrdinal { get; internal set; }
        public long FixedTicks { get; internal set; }
        public string Unavailable { get; internal set; }
        public string Error { get; internal set; }
        public ActivationRead<bool> DynamicBody { get; internal set; } = new ActivationRead<bool>("hull_not_readable");
        public ActivationRead<bool> ActiveSimulation { get; internal set; } = new ActivationRead<bool>("dynamic_body_not_confirmed");
        public ActivationRead<uint> BodyFlag { get; internal set; } = new ActivationRead<uint>("dynamic_body_not_confirmed");
        public ActivationRead<bool> PhysicsState { get; internal set; } = new ActivationRead<bool>("dynamic_body_not_confirmed");
        public ActivationRead<uint> PhysicsDescBodyFlag { get; } = new ActivationRead<uint>("omitted_root_physics_definition_precondition_unverified");
        public ActivationRead<bool> KinematicBody { get; } = new ActivationRead<bool>("omitted_hull_getter_preconditions_unverified");
        public ActivationRead<bool> StaticBody { get; } = new ActivationRead<bool>("omitted_hull_getter_preconditions_unverified");
        public ActivationRead<bool> EngineBodySleeping { get; } = new ActivationRead<bool>("omitted_hull_getter_preconditions_unverified");
        public ActivationRead<bool> DynamicBodyStationary { get; } = new ActivationRead<bool>("omitted_disabled_hull_preconditions_unverified");
    }

    public void Reject(string reason)
    {
        // Damage callbacks can run on a physics worker; body changes belong to the mission tick.
        System.Threading.Interlocked.CompareExchange(ref blocker, reason, null);
    }

    public string SetHeldHelm(int ship, bool take)
    {
        if (manifest.Mode != NavalLabMode.HeldHelm) return "rejected:wrong_mode";
        if (Blocker != null || ship < 0 || ship >= Ships.Length || manifest.Controllers[ship] != ownControllerId)
            return "rejected:not_original_owner_or_unavailable";
        var machine = Ships[ship]?.ShipControllerMachine;
        var point = machine?.PilotStandingPoint;
        if (point == null || !point.GameEntity.IsValid || machine.AttachedShip != Ships[ship])
            return "rejected:standing_point_unavailable";
        if (!helmLifecycleInitialized[ship]) return "rejected:helm_lifecycle_unavailable";
        int index = ship * NavalLabManifest.CrewPerShip;
        var agent = Agents.Length > index ? Agents[index] : null;
        if (agent == null || agent.Pointer == UIntPtr.Zero || !agent.IsActive() || agent != Mission.MainAgent
            || !agent.IsPlayerControlled || Ships[ship].Formation == null || agent.Formation != Ships[ship].Formation)
            return "rejected:main_agent_unavailable";
        if (GameNetwork.IsClientOrReplay) return "rejected:native_network_client_or_replay";
        if (point.UserAgent != null && point.UserAgent != agent) return "rejected:foreign_occupant";
        if (agent.CurrentlyUsedGameObject != null && agent.CurrentlyUsedGameObject != point)
            return "rejected:agent_using_other_object";
        if ((point.UserAgent == agent) != (agent.CurrentlyUsedGameObject == point))
            return "rejected:inconsistent_use_identity";
        if (!take)
        {
            if (point.UserAgent == null) return heldHelmAgent == agent && heldHelmPoint == point ? ReleaseHeldHelm() : "already_released";
            if (heldHelmAgent != agent || heldHelmPoint != point) return "rejected:unowned_use";
            return ReleaseHeldHelm();
        }
        // UseGameObject repeats OnUse even for the same user, so never repeat the native call or placement.
        if (point.UserAgent == agent) return heldHelmAgent == agent && heldHelmPoint == point ? "already_taken" : "rejected:unowned_use";
        if (heldHelmAgent == agent && heldHelmPoint == point) ReleaseHeldHelm();
        if (point.HasAIMovingTo || point.MovingAgent != null) return "rejected:standing_point_reserved";
        if (controlledAgent != null || heldHelmAgent != null) return "rejected:control_active";
        if (point.IsDeactivated || point.IsDisabledForPlayers || machine.IsAttachedShipVacant()) return "rejected:helm_disabled_or_ship_vacant";
        heldHelmAgent = agent;
        heldHelmPoint = point;
        heldHelmDeadline = ControlNow + 30;
        try
        {
            helmTakeCalls++;
            agent.UseGameObject(point);
            if (point.UserAgent != agent || agent.CurrentlyUsedGameObject != point || machine.PilotAgent != agent)
                throw new InvalidOperationException("Native helm use identity was not established.");
            // Vanilla's initial pilot placement only, never a moving-deck support correction.
            machine.OnPilotAssignedDuringSpawn();
            return heldHelmStatus = "taken";
        }
        catch (Exception exception)
        {
            ReleaseHeldHelm();
            Reject("helm.take_failed:" + exception.GetType().FullName);
            return heldHelmStatus = "failed:" + exception.GetType().FullName;
        }
    }

    internal bool SuppressHeldCapture(ShipControllerMachine machine)
    {
        if (!RequiresProcessExit || machine == null) return false;
        int slot = IsTwoClientNative ? Array.FindIndex(Ships, candidate => candidate?.ShipControllerMachine == machine)
            : Array.IndexOf(manifest.Controllers, ownControllerId);
        if (slot < 0 || slot >= Ships.Length) return false;
        var ship = Ships[slot];
        if (ship == null || ship.ShipControllerMachine != machine || machine.AttachedShip != ship) return false;
        Reject("helm.capture_suppressed");
        return true;
    }

    internal void BeforeHeldHelmTick(ShipControllerMachine machine)
    {
        if (manifest.Mode != NavalLabMode.HeldHelm || heldHelmAgent == null) return;
        int slot = Array.IndexOf(manifest.Controllers, ownControllerId);
        if (slot < 0 || slot >= Ships.Length || Ships[slot]?.ShipControllerMachine != machine) return;
        if (machine.PilotStandingPoint == heldHelmPoint && heldHelmPoint.UserAgent == null
            && heldHelmAgent.CurrentlyUsedGameObject == null)
        {
            RetireHeldHelm();
            heldHelmStatus = "released:native_stop";
            return;
        }
        if (machine.PilotStandingPoint != heldHelmPoint || heldHelmPoint.UserAgent != heldHelmAgent
            || heldHelmAgent.CurrentlyUsedGameObject != heldHelmPoint)
        {
            Reject("helm.use_identity_changed_before_tick");
            return;
        }
        if (heldHelmPoint.IsDeactivated || heldHelmPoint.IsDisabledForPlayers || machine.IsAttachedShipVacant())
        {
            // Failed cleanup retains the lease; the separate capture-branch barrier still applies this tick.
            string result = ReleaseHeldHelm();
            if (result == "released") heldHelmStatus = "released:helm_disabled_or_ship_vacant";
            else Reject("helm.pre_tick_release_failed:" + result);
        }
    }

    private string ReleaseHeldHelm()
    {
        var agent = heldHelmAgent;
        var point = heldHelmPoint;
        if (agent == null) return "already_released";
        try
        {
            if (agent.Pointer == UIntPtr.Zero || !agent.IsActive() || point == null || !point.GameEntity.IsValid)
                return heldHelmStatus = "unavailable:release_identity";
            if (agent.CurrentlyUsedGameObject == null && point.UserAgent == null) return heldHelmStatus = "already_released";
            // StopUsingGameObject clears UserAgent unconditionally; never clear another user's occupation.
            if (agent.CurrentlyUsedGameObject != point || (point.UserAgent != null && point.UserAgent != agent))
                return heldHelmStatus = "rejected:release_identity_changed";
            helmReleaseCalls++;
            agent.StopUsingGameObject();
            return heldHelmStatus = agent.CurrentlyUsedGameObject == null && point.UserAgent == null
                ? "released" : "failed:release_not_confirmed";
        }
        catch (Exception exception)
        {
            Reject("helm.release_failed:" + exception.GetType().FullName);
            return heldHelmStatus = "failed:" + exception.GetType().FullName;
        }
        finally
        {
            if (agent.CurrentlyUsedGameObject == null && point != null && point.UserAgent == null) RetireHeldHelm();
        }
    }

    private void RetireHeldHelm()
    {
        var agent = heldHelmAgent;
        var point = heldHelmPoint;
        RecordHelmTrace(agent, point, "retirement:before");
        heldHelmAgent = null;
        heldHelmPoint = null;
        heldHelmDeadline = 0;
        RecordHelmTrace(agent, point, "retirement:after");
    }

    public override void OnObjectUsed(Agent userAgent, UsableMissionObject usableGameObject)
    {
        RecordHelmTrace(userAgent, usableGameObject, "use:callback");
        if (userAgent == heldHelmAgent && usableGameObject == heldHelmPoint) helmUseCallbacks++;
        if (IsTwoClientNative && userAgent == nativeHelmAgent && usableGameObject == nativeHelmPoint) nativeHelmUseCallbacks++;
    }

    public override void OnObjectStoppedBeingUsed(Agent userAgent, UsableMissionObject usableGameObject)
    {
        RecordHelmTrace(userAgent, usableGameObject, "stop:callback");
        if (userAgent == heldHelmAgent && usableGameObject == heldHelmPoint) helmStopCallbacks++;
        if (IsTwoClientNative && userAgent == nativeHelmAgent && usableGameObject == nativeHelmPoint) nativeHelmStopCallbacks++;
    }

    private object InspectHeldHelm() => new
    {
        supported = manifest.Mode == NavalLabMode.HeldHelm,
        remoteUseReplication = "unimplemented",
        keyboardViewInputForwarding = "unimplemented",
        status = heldHelmStatus,
        remainingSeconds = heldHelmAgent == null ? 0 : Math.Max(0, heldHelmDeadline - ControlNow),
        helmTakeCalls, helmReleaseCalls, helmUseCallbacks, helmStopCallbacks,
        trace = HelmTrace, traceLimit = HelmTraceLimit,
        ships = Ships.Select((ship, slot) => InspectHelmIdentity(ship, slot)).ToArray()
    };

    private object InspectHelmIdentity(MissionShip ship, int slot)
    {
        try
        {
            var machine = ship?.ShipControllerMachine;
            var point = machine?.PilotStandingPoint;
            if (point == null || !point.GameEntity.IsValid)
                return new { slot, unavailable = "standing_point_unavailable" };
            return new
            {
                slot, shipId = manifest.Ships[slot], owner = manifest.Controllers[slot],
                ownerLocal = manifest.Controllers[slot] == ownControllerId,
                lifecycleInitialized = IsSingleClientNative ? nativeDeploymentCallbacks == 1 && ship.IsDeployed : helmLifecycleInitialized[slot],
                standingPointId = point.Id.Id, standingPointCreatedAtRuntime = point.Id.CreatedAtRuntime,
                standingPointPointer = point.GameEntity.Pointer.ToUInt64(),
                point.IsDisabledForPlayers,
                user = InspectHelmAgent(point.UserAgent), pilot = InspectHelmAgent(machine.PilotAgent),
                captain = InspectHelmAgent(ship.Captain), mainAgent = InspectHelmAgent(Mission.MainAgent),
                mainAgentUsingThisPoint = Mission.MainAgent != null && Mission.MainAgent.CurrentlyUsedGameObject == point,
                mainAgentUsedObjectId = Mission.MainAgent?.CurrentlyUsedGameObject?.Id.Id,
                mainAgentUsedObjectType = Mission.MainAgent?.CurrentlyUsedGameObject?.GetType().FullName
            };
        }
        catch (Exception exception) { return new { slot, error = exception.GetType().FullName }; }
    }

    private object InspectHelmAgent(Agent agent)
    {
        if (agent == null) return new { unavailable = "no_agent" };
        if (agent.Pointer == UIntPtr.Zero) return new { unavailable = "invalid_agent" };
        int slot = Array.IndexOf(Agents, agent);
        return new
        {
            combatantId = slot >= 0 ? (Guid?)manifest.Combatants[slot] : null,
            nativeIndex = agent.Index, pointer = agent.Pointer.ToUInt64(),
            agent.IsMainAgent, agent.IsPlayerControlled,
            usedObjectId = agent.CurrentlyUsedGameObject?.Id.Id,
            usedObjectType = agent.CurrentlyUsedGameObject?.GetType().FullName
        };
    }

    public string StartAgentControl(string kind, int ship, float value)
    {
        if (IsTwoClientNative) return "rejected:keyboard_controls_only";
        if (kind != "walk" && kind != "turn" && kind != "jump" && kind != "crew") return "rejected:unknown_control";
        if (Blocker != null || ship < 0 || ship >= Ships.Length || manifest.Controllers[ship] != ownControllerId)
            return "rejected:not_original_owner_or_unavailable";
        if (float.IsNaN(value) || float.IsInfinity(value) || Math.Abs(value) > 1) return "rejected:invalid_control";
        if (controlledAgent != null) return "rejected:agent_control_active";
        int index = (ship * NavalLabManifest.CrewPerShip) + (kind == "crew" ? 1 : 0);
        var agent = Agents[index];
        if (agent == null || agent.Pointer == UIntPtr.Zero || !agent.IsActive()
            || (kind == "crew" ? !agent.IsAIControlled : !agent.IsPlayerControlled)) return "rejected:agent_unavailable";
        if (kind == "crew" && (Mission.IsTeleportingAgents || Ships[ship].InnerDeckLocalFrames.Count <= NavalLabManifest.CrewPerShip
            || agent.GetScriptedFlags() != Agent.AIScriptedFrameFlags.None)) return "rejected:deck_target_or_unscripted_crew_unavailable";
        if (kind == "jump" && (agent.EventControlFlags & Agent.EventControlFlag.Jump) != 0)
            return "rejected:jump_input_busy";
        controlledAgent = agent;
        controlKind = kind;
        controlShip = ship;
        controlValue = value;
        controlDeadline = ControlNow + 1;
        previousInput = agent.MovementInputVector;
        writtenInput = previousInput;
        wasWalking = agent.WalkMode;
        jumpWritten = false;
        return "applied";
    }

    public void TickAgentControl(float dt)
    {
        TickNativeHelm();
        TickHelmOccupancy();
        TickAxesPulse();
        if (heldHelmAgent != null && (ControlNow >= heldHelmDeadline || Blocker != null)) ReleaseHeldHelm();
        if (controlledAgent == null) return;
        if (ControlNow >= controlDeadline || Blocker != null || float.IsNaN(dt) || float.IsInfinity(dt) || dt < 0) { CancelAgentControl(); return; }
        var agent = controlledAgent;
        if (agent.Pointer == UIntPtr.Zero || !agent.IsActive()) { CancelAgentControl(); return; }
        if (controlKind == "walk")
        {
            writtenInput = new Vec2(0, controlValue);
            agent.MovementInputVector = writtenInput;
            agent.EventControlFlags |= Agent.EventControlFlag.Walk;
        }
        else if (controlKind == "turn") agent.LookDirectionAsAngle += controlValue * Math.Min(dt, 0.1f);
        else if (controlKind == "jump")
        {
            if (!jumpWritten) { agent.EventControlFlags |= Agent.EventControlFlag.Jump; jumpWritten = true; }
            else agent.EventControlFlags &= ~Agent.EventControlFlag.Jump;
        }
        else
        {
            if (Mission.IsTeleportingAgents) { Reject("crew control entered native teleport mode"); CancelAgentControl(); return; }
            var ship = Ships[controlShip];
            // Slot five is native-enumerated and not one of the five initial spawn slots; no relocation is used.
            var target = ship.GlobalFrame.TransformToParent(ship.InnerDeckLocalFrames[NavalLabManifest.CrewPerShip]).origin;
            var position = new WorldPosition(Mission.Scene, target);
            if (position.GetNavMesh() == UIntPtr.Zero) { Reject("crew deck target has no native navmesh"); CancelAgentControl(); return; }
            agent.SetScriptedPosition(ref position, false);
        }
    }

    private void CancelAgentControl()
    {
        var agent = controlledAgent;
        controlledAgent = null;
        if (agent == null || agent.Pointer == UIntPtr.Zero || !agent.IsActive()) return;
        if (controlKind == "walk")
        {
            if (agent.MovementInputVector == writtenInput) agent.MovementInputVector = previousInput;
            agent.EventControlFlags &= ~Agent.EventControlFlag.Walk;
            if (!wasWalking) agent.EventControlFlags |= Agent.EventControlFlag.Run;
        }
        if (jumpWritten) agent.EventControlFlags &= ~Agent.EventControlFlag.Jump;
        if (controlKind == "crew") agent.DisableScriptedMovement();
    }

    public void CancelControls()
    {
        CancelAxesPulse("cancelled_safety_stop");
        CancelPendingNativeHelm();
        CancelAgentControl();
        ReleaseHeldHelm();
        if ((IsSingleClientNative && !nativeTerminalHold) || IsTwoClientNative) CancelNativeControls();
        foreach (var ship in Ships.Where(ship => ship?.Controller is PlayerShipController && (!IsTwoClientNative || factoryHost)))
        {
            var input = ShipInputRecord.None();
            input.SetRudderLateral(0);
            input.SetRowerLongitudinal(RowerLongitudinalInput.Stop);
            ((PlayerShipController)ship.Controller).SetInput(in input);
        }
    }

    internal object InspectShipInventory(MissionShip ship)
    {
        if (ship == null) return new { error = "ship_not_created" };
        var entity = ship.GameEntity;
        if (!entity.IsValid) return new { error = "invalid_ship_entity", fixtureSlot = Array.IndexOf(Ships, ship) };
        return new
        {
            entity = entity.Name,
            nativePointer = entity.Pointer.ToUInt64(),
            scriptType = ship.GetType().FullName,
            missionObjectId = ship.Id.Id,
            createdAtRuntime = ship.Id.CreatedAtRuntime,
            fixtureSlot = Array.IndexOf(Ships, ship),
            registered = Mission.MissionObjects.Contains(ship),
            origin = ship.ShipOrigin?.GetType().FullName,
            ship.IsInitialized,
            actuatorsInitialized = ship._actuators != null,
            physicsBound = ship.Physics != null,
            physicsInitialized = ship.Physics?.IsInitialized == true,
            hasDynamicBody = entity.HasDynamicRigidBody(),
            activeSimulation = entity.HasDynamicRigidBodyAndActiveSimulation(),
            formationBound = ship.Formation != null
        };
    }

    public object Inspect() => new
    {
        mode = manifest.Mode.ToString(),
        singleClientNative = IsSingleClientNative ? InspectNativeControls() : null,
        twoClientNative = IsTwoClientNative ? InspectNativeControls() : null,
        factoryAuthorityProbe = InspectFactoryProbe(),
        heldHelm = InspectHeldHelm(),
        syntheticCrew = true,
        agentControl = controlledAgent == null ? "inactive" : controlKind,
        controlledCombatant = controlledAgent == null ? (Guid?)null : manifest.Combatants[Array.IndexOf(Agents, controlledAgent)],
        startup = StartupDiagnostics,
        activationTransitions = ActivationTransitions,
        manifest.InstanceId,
        manifest.IncarnationId,
        Simulating,
        Blocker,
        forceApplications = System.Threading.Interlocked.Read(ref ForceApplications),
        fixedTicks = System.Threading.Interlocked.Read(ref FixedTicks),
        activeFixedTicks = System.Threading.Interlocked.Read(ref ActiveFixedTicks),
        expectedShipCount = manifest.Ships.Length,
        expectedAgentCount = manifest.Combatants.Length,
        ships = Ships.Select(InspectShip).ToArray(),
        agents = Agents.Select(InspectAgent).ToArray()
    };

    internal NavalLabShipSnapshot InspectShip(MissionShip ship, int index)
    {
        var id = manifest.Ships[index];
        if (ship == null) return new NavalLabShipSnapshot(id, "ship_not_created");
        if (!ship.GameEntity.IsValid) return new NavalLabShipSnapshot(id, "invalid_ship_entity");
        if (!ship.IsInitialized || ship.Physics?.IsInitialized != true)
            return new NavalLabShipSnapshot(id, "ship_not_initialized");
        try
        {
            var controller = ship.Controller as PlayerShipController;
            return new NavalLabShipSnapshot(id, ship.GlobalFrame, ship.Physics.LinearVelocity,
                ship.Physics.AngularVelocity, ship.Physics._committedTotalMass,
                ship.Physics._committedWeightedAgentsPosition, ship.GameEntity.HasDynamicRigidBody(),
                ship.GameEntity.HasDynamicRigidBodyAndActiveSimulation(), ship.InnerDeckLocalFrames.Count,
                controller?._inputRecord.RudderLateral, controller?._inputRecord.RowerLongitudinal.ToString());
        }
        catch (Exception exception)
        {
            return new NavalLabShipSnapshot(id, exception.ToString());
        }
    }

    internal NavalLabAgentSnapshot InspectAgent(Agent agent, int index)
    {
        var id = manifest.Combatants[index];
        var controller = manifest.Controllers[index / NavalLabManifest.CrewPerShip];
        bool captain = agent != null && Ships.Length > index / NavalLabManifest.CrewPerShip
            && Ships[index / NavalLabManifest.CrewPerShip]?.Captain == agent;
        if (agent == null || agent.Pointer == UIntPtr.Zero)
            return new NavalLabAgentSnapshot(id, controller, captain, "invalid_agent");
        try
        {
            var support = agent.GetSteppedRootEntity();
            int supportSlot = support.IsValid
                ? Array.FindIndex(Ships, ship => ship != null && ship.GameEntity == support) : -1;
            return new NavalLabAgentSnapshot(id, controller, captain, agent.Position, agent.Health,
                agent.GetTotalMass(), support.IsValid ? support.Name : null, support.IsValid, supportSlot,
                agent.GetWorldPosition().GetNavMesh().ToUInt64(),
                supportSlot >= 0 ? Ships[supportSlot].GlobalFrame.TransformToLocal(agent.Position) : (Vec3?)null,
                agent.LookDirection, new Vec3(agent.MovementInputVector.x, agent.MovementInputVector.y, 0));
        }
        catch (Exception exception)
        {
            return new NavalLabAgentSnapshot(id, controller, captain, exception.ToString());
        }
    }
}
#endif
