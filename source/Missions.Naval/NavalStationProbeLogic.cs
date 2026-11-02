#if DEBUG
using Common;
using Common.Commands;
using Common.Logging;
using Common.Messaging;
using Missions.Battles;
using Missions.Messages;
using NavalDLC.Missions;
using NavalDLC.Missions.Objects;
using NavalDLC.Missions.Objects.UsableMachines;
using Newtonsoft.Json;
using Serilog;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TaleWorlds.Engine;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace Missions.Naval;

/// <summary>Owns station observations and a bounded timed capture for one naval mission.</summary>
public sealed class NavalStationProbeLogic : MissionLogic
{
    private static readonly ILogger Logger = LogManager.GetLogger<NavalStationProbeLogic>();
    internal const int SampleEveryFrames = 5;
    private readonly CoopBattleController controller;
    private readonly INavalShipEngine engine;
    private readonly IMessageBroker broker;
    private readonly IAgentStationUseReplicator replicator;
    private readonly Dictionary<Agent, SeatActivity> activity = new Dictionary<Agent, SeatActivity>();
    private readonly Dictionary<UsableMissionObject, StationIdentity> stationIdentities = new Dictionary<UsableMissionObject, StationIdentity>();
    private StationProbeCapture capture;
    private Guid watchedHull;
    private float stopAt;
    private string outputPath;
    private long frame;
    private volatile bool ended;
    private Guid reseatHull;
    private int reseatIndex = -1;

    /// <summary>Observes existing use/release messages and the replicator's recovery/pin calls.</summary>
    public NavalStationProbeLogic(CoopBattleController controller, INavalShipEngine engine, IMessageBroker broker,
        IAgentStationUseReplicator replicator)
    {
        this.controller = controller;
        this.engine = engine;
        this.broker = broker;
        this.replicator = replicator;
        broker.Subscribe<AgentStationUseChanged>(OnStationChanged);
        replicator.StationProbeEvent += Observe;
    }

    /// <summary>Reads matching live users or schedules a bounded watch or authoritative re-seat.</summary>
    internal CoopCommandResult Execute(string mode, string prefix, int value)
    {
        var ships = controller.MissionComponent.ShipRegistry.Ships;
        if (mode == "snapshot")
        {
            var matches = ships.Where(ship => prefix == null || ship.ShipId.ToString().StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                .Select(ship => ship.ShipId).ToArray();
            if (prefix != null && matches.Length == 0) return Failed("No registered hull matches that prefix.");
            return Success(new { time = Mission.CurrentTime, frame, agents = Snapshot(matches) });
        }

        var error = NavalReswapCoopCommand.TryResolveShipId(ships.Select(ship => ship.ShipId), prefix, out var hullId);
        if (error != null) return Failed(error);
        if (!Mission.IsDeploymentFinished) return Failed("Wait until deployment finishes.");
        if (mode == "reseat")
        {
            error = ValidateReseat(hullId, value, out _, out _);
            if (error != null) return Failed(error);
            if (reseatIndex >= 0) return Failed("A station re-seat is already queued.");
            reseatHull = hullId;
            reseatIndex = value;
            return Success(new { queued = "reseat", hullId, agentIndex = value });
        }

        if (!controller.MissionComponent.ShipRegistry.TryGet(hullId, out var info)
            || info.Hull is not MissionShip hull || hull.IsRemoved || !hull.GameEntity.IsValid)
            return Failed("The hull is not a live naval ship.");
        if (capture != null) return Failed("A station watch is already running; wait for its timed capture to finish.");
        watchedHull = hullId;
        stopAt = Mission.CurrentTime + value;
        // CoopMod.SetupLogging also resolves its log filename against the process working directory.
        outputPath = System.IO.Path.GetFullPath(
            "station_probe_" + hullId.ToString("N") + "_" + DateTime.UtcNow.ToString("yyyyMMdd_HHmmss_fffffff") + ".json");
        capture = new StationProbeCapture(hullId, controller.Session.OwnControllerId, Mission.CurrentTime, value);
        capture.Add(new { kind = "snapshot", time = Mission.CurrentTime, frame, agents = Snapshot(new[] { hullId }) });
        try { WriteCapture("watching"); }
        catch (Exception exception)
        {
            capture = null;
            Logger.Error(exception, "Starting the naval station watch");
            return Failed("Could not write the station capture: " + exception.Message);
        }
        return Success(new { watching = hullId, seconds = value, sampleEveryFrames = SampleEveryFrames, file = outputPath });
    }

    /// <summary>Checks both hull and agent authority again at the tick that performs the re-seat.</summary>
    private string ValidateReseat(Guid hullId, int index, out Agent agent, out UsableMissionObject point)
    {
        agent = Mission.Agents.FirstOrDefault(candidate => candidate.Index == index && candidate.IsActive());
        point = agent?.CurrentlyUsedGameObject;
        if (!controller.MissionComponent.ShipRegistry.TryGet(hullId, out var ship)
            || ship.Hull is not MissionShip hull || hull.IsRemoved || hull.IsSinking || !hull.GameEntity.IsValid)
            return "The hull is not live and afloat.";
        if (ship.CurrentAuthority != controller.Session.OwnControllerId || agent == null
            || !controller.MissionComponent.AgentRegistry.IsLocallyControlled(agent))
            return "Run reseat on both the hull's and the agent's authority client; agent indices are local.";
        if (!engine.TryDescribeStation(point, out var stationHull, out _, out _) || stationHull != hull
            || !engine.IsSeated(agent, point)) return "The agent is not using a station on this hull.";
        return null;
    }

    /// <summary>Advances the diagnostic frame before the naval services emit their pin events.</summary>
    public override void OnPreMissionTick(float dt)
    {
        base.OnPreMissionTick(dt);
        frame++;
    }

    /// <summary>Runs mutations outside the async agent tick and samples the watch every fifth mission frame.</summary>
    public override void OnMissionTick(float dt)
    {
        base.OnMissionTick(dt);
        try
        {
            if (reseatIndex >= 0)
            {
                int index = reseatIndex;
                reseatIndex = -1;
                var error = ValidateReseat(reseatHull, index, out var agent, out var point);
                if (error != null) Logger.Warning("Station probe re-seat refused: {Reason}", error);
                else
                {
                    var before = ReadAgent(agent, point);
                    replicator.DebugReseat(agent, point);
                    Logger.Information("NAVAL_STATION_PROBE_RESEAT {Json}", JsonConvert.SerializeObject(new
                    { time = Mission.CurrentTime, before, after = ReadAgent(agent, point), seated = engine.IsSeated(agent, point) }));
                }
            }
            if (capture == null) return;
            if (frame % SampleEveryFrames == 0 || Mission.CurrentTime >= stopAt)
                capture.Add(new { kind = "snapshot", time = Mission.CurrentTime, frame, agents = Snapshot(new[] { watchedHull }) });
            if (Mission.CurrentTime >= stopAt) Finish("elapsed");
        }
        catch (Exception exception)
        {
            Logger.Error(exception, "Sampling the naval station probe");
        }
    }

    /// <summary>Preserves hook time when a use/release originates on an agent worker and must be observed later.</summary>
    private void OnStationChanged(MessagePayload<AgentStationUseChanged> payload)
    {
        if (ended) return;
        var change = payload.What;
        float time = Mission.CurrentTime;
        GameThread.RunSafe(() => RecordEvent(change.Agent, change.Point, change.InUse ? "use" : "release", time),
            context: nameof(OnStationChanged));
    }

    /// <summary>Tracks every recovery attempt and actual locked-frame pin, even while no watch is running.</summary>
    private void Observe(Agent agent, UsableMissionObject point, string kind)
    {
        try
        {
            if (ended) return;
            if (!activity.TryGetValue(agent, out var state)) activity[agent] = state = new SeatActivity();
            if (kind == "reseat") state.Reseats++;
            if (kind == "pin" && point.LockUserFrames) state.LastPinFrame = frame;
            RecordEvent(agent, point, kind, Mission.CurrentTime);
        }
        catch (Exception exception)
        {
            Logger.Error(exception, "Observing a naval station probe event");
        }
    }

    /// <summary>Records local events on the watched hull without altering the engine or sending messages.</summary>
    private void RecordEvent(Agent agent, UsableMissionObject point, string kind, float time)
    {
        if (ended || capture == null || agent == null || point == null) return;
        bool described = engine.TryDescribeStation(point, out var hull, out var key, out int pointIndex)
            && controller.MissionComponent.ShipRegistry.TryGetByHull(hull, out _);
        if (described)
        {
            controller.MissionComponent.ShipRegistry.TryGetByHull(hull, out var ship);
            stationIdentities[point] = new StationIdentity(ship.ShipId, ship.ShipId + "/" + key + "#" + pointIndex);
        }
        if (!stationIdentities.TryGetValue(point, out var identity) || identity.HullId != watchedHull) return;
        controller.MissionComponent.AgentRegistry.TryGetAgentInfo(agent, out var info);
        capture.Add(new { kind, time, observedAt = Mission.CurrentTime, frame, agentId = info?.AgentId,
            agentIndex = agent.Index, hullId = identity.HullId, station = identity.Station, identityFromLastObservation = !described,
            pinApplied = kind == "pin" ? (bool?)point.LockUserFrames : null,
            state = kind != "pin" && agent.IsActive() ? ReadAgent(agent, point) : null });
    }

    /// <summary>Includes every active station user whose current station belongs to a matching registered hull.</summary>
    private StationProbeSample[] Snapshot(Guid[] hulls) => Mission.Agents.Where(agent => agent.IsActive()
        && agent.CurrentlyUsedGameObject != null).Select(agent => ReadAgent(agent, agent.CurrentlyUsedGameObject))
        .Where(sample => sample != null && hulls.Contains(sample.HullId)).ToArray();

    /// <summary>Reads physical support, logical use and placement independently so a matching seat cannot hide drift.</summary>
    private StationProbeSample ReadAgent(Agent agent, UsableMissionObject point)
    {
        if (!point.GameEntity.IsValid || !engine.TryDescribeStation(point, out var stationHull, out var stationKey, out int pointIndex)
            || stationHull is not MissionShip hull || !hull.GameEntity.IsValid || hull.IsRemoved
            || !controller.MissionComponent.ShipRegistry.TryGetByHull(hull, out var ship)) return null;
        controller.MissionComponent.AgentRegistry.TryGetAgentInfo(agent, out var info);
        var userFrame = point.GetUserFrameForAgent(agent);
        stationIdentities[point] = new StationIdentity(ship.ShipId, ship.ShipId + "/" + stationKey + "#" + pointIndex);
        var hullFrame = hull.GlobalFrame;
        var world = agent.Position;
        var machine = NavalShipEngine.PilotMachineOf(point);
        var pilotFrame = point.GameEntity.GetGlobalFrame();
        Vec3? displacement = null;
        Vec3? spawnPosition = null;
        if (machine is ShipOarMachine oar)
        {
            displacement = MBAnimation.GetAnimationDisplacementAtProgress(
                MBActionSet.GetAnimationIndexOfAction(agent.ActionSet, in oar._rowSitDownActionIndex), 1f);
            pilotFrame.rotation.Orthonormalize();
            spawnPosition = pilotFrame.TransformToParent(displacement.Value);
        }
        else if (machine is ShipControllerMachine) spawnPosition = pilotFrame.origin;
        var parent = MBAPI.IMBAgent.GetAgentParentEntity(agent.GetPtr());
        var stepped = agent.GetSteppedEntity();
        var steppedRoot = agent.GetSteppedRootEntity();
        var steppedShip = agent.GetComponent<AgentNavalComponent>()?.SteppedShip;
        NetworkShipInfo supportShip = null;
        if (steppedShip != null) controller.MissionComponent.ShipRegistry.TryGetByHull(steppedShip, out supportShip);
        activity.TryGetValue(agent, out var state);
        return new StationProbeSample
        {
            AgentId = info?.AgentId, AgentIndex = agent.Index, AgentAuthority = info?.CurrentAuthority,
            HullId = ship.ShipId, HullAuthority = ship.CurrentAuthority,
            Authority = ship.CurrentAuthority == controller.Session.OwnControllerId ? "owner" : "copy",
            Station = replicator.DescribeStation(agent), UsingStation = agent.CurrentlyUsedGameObject == point,
            PointHasAgent = point.UserAgent == agent, Machine = machine?.GetType().Name,
            WorldPosition = Vector(world), HullLocalPosition = Vector(hullFrame.TransformToLocal(world)),
            UserFrame = new { origin = Vector(userFrame.Origin.GetVec3WithoutValidity()), groundOrigin = Vector(userFrame.Origin.GetGroundVec3()),
                zValidity = userFrame.Origin.State.ToString(),
                side = Vector(userFrame.Rotation.s), forward = Vector(userFrame.Rotation.f), up = Vector(userFrame.Rotation.u) },
            AnimationDisplacement = displacement.HasValue ? Vector(displacement.Value) : null,
            ExpectedSpawnPilotPosition = spawnPosition.HasValue ? Vector(spawnPosition.Value) : null,
            SteppedShipId = supportShip?.ShipId, SteppedShipPresent = steppedShip != null,
            SteppedEntity = Entity(stepped, hull), SteppedRootEntity = Entity(steppedRoot, hull),
            NativeParent = parent == null ? null : Entity(parent.WeakEntity, hull),
            SteppedBodyFlags = agent.GetSteppedBodyFlags().ToString(),
            MovementLock = agent.MovementLockedState.ToString(), LockUserFrames = point.LockUserFrames,
            LockUserPositions = point.LockUserPositions, AgentFlags = agent.GetAgentFlags().ToString(),
            ScriptedFlags = agent.GetScriptedFlags().ToString(),
            Actions = new[] { 0, 1 }.Select(channel => new { channel, index = agent.GetCurrentAction(channel).Index,
                name = agent.GetCurrentAction(channel).GetName(), progress = agent.GetCurrentActionProgress(channel) }).ToArray(),
            PinnedThisFrame = state != null && state.LastPinFrame == frame, ReseatCount = state?.Reseats ?? 0,
        };
    }

    /// <summary>Uses numeric XYZ fields rather than locale-dependent vector strings.</summary>
    internal static object Vector(Vec3 value) => new { x = value.x, y = value.y, z = value.z };

    /// <summary>Names native support entities and reports their path only when they belong to this hull.</summary>
    private object Entity(WeakGameEntity entity, MissionShip hull) => !entity.IsValid ? null : new
    { name = entity.Name, pathOnHull = NavalShipEngine.EntityPath(entity, hull.GameEntity), rootName = entity.Root.Name };

    /// <summary>Serializes a bounded capture in the same working directory as Coop_client.log.</summary>
    private void WriteCapture(string reason) => File.WriteAllText(outputPath, capture.ToJson(reason, Mission.CurrentTime));

    /// <summary>Flushes once at duration expiry or mission end and releases the ring's records.</summary>
    private void Finish(string reason)
    {
        if (capture == null) return;
        try
        {
            WriteCapture(reason);
            Logger.Information("NAVAL_STATION_PROBE_SAVED {File}", outputPath);
        }
        finally { capture = null; }
    }

    /// <summary>Unsubscribes before teardown so queued worker observations cannot access an ended mission.</summary>
    public override void OnEndMissionInternal()
    {
        ended = true;
        broker.Unsubscribe<AgentStationUseChanged>(OnStationChanged);
        replicator.StationProbeEvent -= Observe;
        try { Finish("mission_ended"); }
        catch (Exception exception) { Logger.Error(exception, "Saving the naval station probe"); }
        activity.Clear();
        stationIdentities.Clear();
        base.OnEndMissionInternal();
    }

    /// <summary>Formats command JSON consistently with the existing naval diagnostics.</summary>
    private CoopCommandResult Success(object value) => new CoopCommandResult(true, "NAVAL_STATION_PROBE " + JsonConvert.SerializeObject(value));

    /// <summary>Returns a dispatcher-visible refusal without touching station state.</summary>
    private CoopCommandResult Failed(string error) => new CoopCommandResult(false, error, "command_failed");

    /// <summary>Retains per-agent diagnostic counters for this mission only.</summary>
    private sealed class SeatActivity
    {
        public long LastPinFrame = -1;
        public long Reseats;
    }

    /// <summary>Keeps the last describable identity so a release after detachment remains attributable.</summary>
    private sealed class StationIdentity
    {
        /// <summary>Stores a content path and registry hull id without retaining a native hull.</summary>
        public StationIdentity(Guid hullId, string station) { HullId = hullId; Station = station; }
        public Guid HullId { get; }
        public string Station { get; }
    }
}

/// <summary>Defines the snapshot JSON schema, including explicit unknown native state.</summary>
[JsonObject(NamingStrategyType = typeof(Newtonsoft.Json.Serialization.CamelCaseNamingStrategy))]
internal sealed class StationProbeSample
{
    public Guid? AgentId;
    public int AgentIndex;
    public string AgentAuthority;
    public Guid HullId;
    public string HullAuthority;
    public string Authority;
    public string Station;
    public bool UsingStation, PointHasAgent;
    public string Machine;
    public object WorldPosition, HullLocalPosition, UserFrame, AnimationDisplacement, ExpectedSpawnPilotPosition;
    public Guid? SteppedShipId;
    public bool SteppedShipPresent;
    public object SteppedEntity, SteppedRootEntity, NativeParent;
    public string SteppedBodyFlags;
    public string MovementLock, AgentFlags, ScriptedFlags;
    public bool LockUserFrames, LockUserPositions;
    public object Actions;
    public bool PinnedThisFrame;
    public long ReseatCount;
    public object ExcludedFromGravity = null;
    public object PhysicsForceClosed = null;
    public object FaceGroupExclusion = null;
    public string Unavailable = "Agent and IMBAgent expose setters but no getters for gravity exclusion, physics force closure and face-group exclusion.";
}

/// <summary>Bounds retained samples and events and preserves their observation order on export.</summary>
internal sealed class StationProbeCapture
{
    internal const int Capacity = 65536;
    private readonly Queue<object> records = new Queue<object>();
    private readonly Guid hullId;
    private readonly string controller;
    private readonly float startedAt;
    private readonly int seconds;
    private long overwritten;

    /// <summary>Captures immutable identity and timing metadata for one watch.</summary>
    public StationProbeCapture(Guid hullId, string controller, float startedAt, int seconds)
    {
        this.hullId = hullId;
        this.controller = controller;
        this.startedAt = startedAt;
        this.seconds = seconds;
    }

    /// <summary>Evicts the oldest record when the ring reaches its fixed bound.</summary>
    internal void Add(object record)
    {
        if (records.Count == Capacity) { records.Dequeue(); overwritten++; }
        records.Enqueue(record);
    }

    /// <summary>Writes a versioned JSON envelope with the retained window and overflow count.</summary>
    internal string ToJson(string reason, float endedAt) => JsonConvert.SerializeObject(new
    { schemaVersion = 1, hullId, controller, startedAt, endedAt, requestedSeconds = seconds,
        sampleEveryFrames = NavalStationProbeLogic.SampleEveryFrames, capacity = Capacity, overwritten, reason, records = records.ToArray() });
}
#endif
