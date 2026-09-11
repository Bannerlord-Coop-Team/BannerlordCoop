#if DEBUG
using Common;
using Missions.Battles;
using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace Missions.Naval;

internal sealed partial class NavalLabBehavior
{
    private const int DriftSampleLimit = 1202;
    private const double DriftSampleInterval = 0.05;
    private Guid driftOperation;
    private int driftSeconds;
    private double driftStarted;
    private double driftLastSample;
    private double driftMaxGap;
    private double driftLastTime;
    private int driftSampleCount;
    private int driftInvalidDt;
    private int driftMissionTicks;
    private double driftMissionDtSum;
    private float driftMaxMissionDt;
    private int driftLongGaps;
    private string driftEnd;
    private DriftRow[] driftRows;

    private sealed class DriftReading
    {
        public double ElapsedSeconds;
        public long MissionTick;
        public string NativeController;
        public long HelmRevision;
        public NavalLabVectorSnapshot ActorOffset;
        public NavalLabVectorSnapshot VisualOffset;
        public NavalLabVectorSnapshot HullOrigin;
        public double AbsoluteHorizontalMeters;
        public double GeometricVerticalMeters;
        public double BaselineDriftMeters;
        public double VisualBaselineDriftMeters;
    }

    private sealed class DriftRow
    {
        internal int Index;
        internal Guid Combatant;
        internal string StationKey;
        internal int PointId;
        internal bool PointCreatedAtRuntime;
        internal StandingPoint Point;
        internal Agent Actor;
        internal int Valid;
        internal int Invalid;
        internal int OccupancyLoss;
        internal string LastError;
        internal Vec3 Baseline;
        internal Vec3 VisualBaseline;
        internal Vec3 HullBaseline;
        internal long BaselineRevision;
        internal string BaselineController;
        internal double MaximumHullDisplacement;
        internal DriftReading First;
        internal DriftReading Last;
        internal DriftReading Worst;
        internal readonly List<double> AbsoluteHorizontal = new();
        internal readonly List<double> AbsoluteVertical = new();
        internal readonly List<double> BaselineDistance = new();
        internal readonly List<double> VisualDistance = new();
    }

    internal object StartDrift(Guid operationId, int seconds)
    {
        if (!GameThread.Instance.IsGameThread || operationId == Guid.Empty || seconds < 1 || seconds > 60)
            throw new InvalidOperationException("drift.invalid_request");
        if (driftOperation != Guid.Empty)
        {
            if (driftOperation != operationId || driftSeconds != seconds) throw new InvalidOperationException("drift.recording_already_exists_export_before_teardown");
            return InspectDrift();
        }
        if (!DriftLifetimeValid() || !HelmReplicasReady || appliedStations.Count != 2)
            throw new InvalidOperationException("drift.requires_ready_deployed_station_and_helm_replicas");
        driftOperation = operationId;
        driftSeconds = seconds;
        driftStarted = ControlNow;
        driftLastSample = driftStarted;
        driftRows = Enumerable.Range(0, 10).Select(index => new DriftRow { Index = index, Combatant = manifest.Combatants[index] }).ToArray();
        SampleDrift(driftStarted);
        return InspectDrift();
    }

    private bool DriftLifetimeValid() => IsTwoClientNative && Mission != null && Mission == Mission.Current
        && !factoryTerminal && !nativeTerminalHold && Blocker == null && nativeDeploymentComplete
        && factoryReleased && CanUseNativeControls && Mission.IsDeploymentFinished && Mission.Mode == MissionMode.Battle;

    public override void OnMissionTick(float dt)
    {
        base.OnMissionTick(dt);
        if (driftRows == null || driftEnd != null) return;
        if (!GameThread.Instance.IsGameThread) { driftEnd = "not_game_thread"; return; }
        if (!DriftLifetimeValid()) { driftEnd = "lifetime_or_terminal"; return; }
        double now = ControlNow;
        if (float.IsNaN(dt) || float.IsInfinity(dt) || dt < 0) { driftInvalidDt++; driftEnd = "invalid_mission_dt"; return; }
        driftMissionTicks++;
        driftMissionDtSum += dt;
        driftMaxMissionDt = Math.Max(driftMaxMissionDt, dt);
        if (now - driftLastSample < DriftSampleInterval && now - driftStarted < driftSeconds) return;
        if (driftSampleCount >= DriftSampleLimit) { driftEnd = "sample_capacity"; return; }
        SampleDrift(now);
        if (now - driftStarted >= driftSeconds) driftEnd = "duration_complete";
    }

    private void SampleDrift(double now)
    {
        if (now - driftLastSample > 0.25) driftLongGaps++;
        driftMaxGap = Math.Max(driftMaxGap, now - driftLastSample);
        driftLastSample = now;
        driftLastTime = now - driftStarted;
        driftSampleCount++;
        foreach (var row in driftRows)
        {
            try { SampleDriftRow(row, now - driftStarted); }
            catch (Exception exception)
            {
                row.Invalid++;
                row.LastError = exception.GetType().Name + ":" + exception.Message;
                if (row.LastError.Length > 160) row.LastError = row.LastError.Substring(0, 160);
            }
        }
    }

    private void SampleDriftRow(DriftRow row, double elapsed)
    {
        int slot = row.Index / NavalLabManifest.CrewPerShip;
        int crew = row.Index % NavalLabManifest.CrewPerShip;
        var ship = Ships[slot];
        var agent = Agents[row.Index];
        StandingPoint point;
        Agent pilot;
        long revision;
        string key;
        if (crew == 0)
        {
            var state = replicatedHelms[slot];
            if (state == null || !state.Occupied || observedHelmRevisions[slot] != state.Revision || confirmedHelmRevisions[slot] != state.Revision)
            {
                row.OccupancyLoss++;
                throw new InvalidOperationException("helm_not_confirmed_occupied");
            }
            var machine = ship.ShipControllerMachine;
            if (machine == null || !machine.GameEntity.IsValid) throw new InvalidOperationException("helm_invalid");
            point = machine.PilotStandingPoint;
            pilot = machine.PilotAgent;
            revision = state.Revision;
            key = "helm";
        }
        else
        {
            if (!appliedStations.TryGetValue(slot, out var stations) || stations.Phase != "commit" || stations.IncarnationId != manifest.IncarnationId)
                throw new InvalidOperationException("oar_manifest_missing");
            key = stations.Keys[crew - 1];
            var machine = StationInventory(slot)[key];
            point = machine.PilotStandingPoint;
            pilot = machine.PilotAgent;
            revision = 0;
        }
        if (agent == null || agent.Pointer == UIntPtr.Zero || !agent.IsActive() || agent.Mission != Mission
            || point == null || point.GetType() != typeof(StandingPoint) || !point.GameEntity.IsValid || !ship.GameEntity.IsValid)
            throw new InvalidOperationException("native_lifetime");
        if (point.UserAgent != agent || pilot != agent || agent.CurrentlyUsedGameObject != point)
        {
            row.OccupancyLoss++;
            throw new InvalidOperationException("occupancy_mismatch");
        }
        if (!point.TranslateUser || !point.LockUserFrames || Mission.IsTeleportingAgents)
            throw new InvalidOperationException("unsupported_user_frame_branch");
        if (row.First != null && (row.Point != point || row.Actor != agent || row.StationKey != key
            || row.BaselineRevision != revision || row.BaselineController != agent.Controller.ToString()))
            throw new InvalidOperationException("station_revision_or_identity_changed");

        // Reconstruct geometry without WorldFrame/GetUserFrame, which refresh the native station cache.
        var station = point.GameEntity.GetGlobalFrame();
        var custom = point.GameEntityWithWorldPosition._customLocalFrame;
        var anchor = station.TransformToParent(in custom);
        anchor.rotation.Orthonormalize();
        RequireDriftFinite(anchor.origin);
        RequireDriftFinite(anchor.rotation.f);
        RequireDriftFinite(anchor.rotation.s);
        RequireDriftFinite(anchor.rotation.u);
        var offset = anchor.TransformToLocal(agent.Position);
        var visual = anchor.TransformToLocal(agent.VisualPosition);
        var hullOrigin = ship.GameEntity.GetGlobalFrame().origin;
        RequireDriftFinite(offset);
        RequireDriftFinite(visual);
        RequireDriftFinite(hullOrigin);
        if (row.First == null)
        {
            row.Baseline = offset;
            row.VisualBaseline = visual;
            row.HullBaseline = hullOrigin;
            row.BaselineRevision = revision;
            row.BaselineController = agent.Controller.ToString();
            row.StationKey = key;
            row.PointId = point.Id.Id;
            row.PointCreatedAtRuntime = point.Id.CreatedAtRuntime;
            row.Actor = agent;
            row.Point = point;
        }
        var reading = new DriftReading
        {
            ElapsedSeconds = elapsed, MissionTick = nativeHelmTicks, NativeController = agent.Controller.ToString(), HelmRevision = revision,
            ActorOffset = new NavalLabVectorSnapshot(offset), VisualOffset = new NavalLabVectorSnapshot(visual), HullOrigin = new NavalLabVectorSnapshot(hullOrigin),
            AbsoluteHorizontalMeters = Math.Sqrt(((double)offset.x * offset.x) + ((double)offset.y * offset.y)),
            GeometricVerticalMeters = Math.Abs(offset.z), BaselineDriftMeters = DriftLength(offset - row.Baseline),
            VisualBaselineDriftMeters = DriftLength(visual - row.VisualBaseline)
        };
        row.Valid++;
        row.First ??= reading;
        row.Last = reading;
        if (row.Worst == null || reading.BaselineDriftMeters > row.Worst.BaselineDriftMeters) row.Worst = reading;
        row.AbsoluteHorizontal.Add(reading.AbsoluteHorizontalMeters);
        row.AbsoluteVertical.Add(reading.GeometricVerticalMeters);
        row.BaselineDistance.Add(reading.BaselineDriftMeters);
        row.VisualDistance.Add(reading.VisualBaselineDriftMeters);
        row.MaximumHullDisplacement = Math.Max(row.MaximumHullDisplacement, DriftLength(hullOrigin - row.HullBaseline));
    }

    private static double DriftLength(Vec3 value) => Math.Sqrt(((double)value.x * value.x) + ((double)value.y * value.y) + ((double)value.z * value.z));
    private static void RequireDriftFinite(Vec3 value)
    {
        if (float.IsNaN(value.x) || float.IsInfinity(value.x) || float.IsNaN(value.y) || float.IsInfinity(value.y)
            || float.IsNaN(value.z) || float.IsInfinity(value.z)) throw new InvalidOperationException("nonfinite_native_vector");
    }

    private static object DriftDistribution(List<double> values)
    {
        if (values.Count == 0) return null;
        var sorted = values.OrderBy(value => value).ToArray();
        return new { count = sorted.Length, max = sorted[sorted.Length - 1], p95 = sorted[(int)Math.Ceiling(sorted.Length * 0.95) - 1] };
    }

    internal object InspectDrift()
    {
        if (!GameThread.Instance.IsGameThread) return new { unavailable = "not_game_thread" };
        if (driftRows == null) return new { unavailable = "not_started" };
        return new
        {
            incarnation = manifest.IncarnationId, operationId = driftOperation, localController = ownControllerId, simulator = factoryHost,
            requestedSeconds = driftSeconds, sampledSpanSeconds = driftLastTime, sampleCount = driftSampleCount,
            secondsSinceLastSample = Math.Max(0, ControlNow - driftLastSample),
            meanSampleIntervalSeconds = driftSampleCount > 1 ? driftLastTime / (driftSampleCount - 1) : 0,
            targetIntervalSeconds = DriftSampleInterval, maxSampleGapSeconds = driftMaxGap, gapsOver250ms = driftLongGaps,
            invalidMissionDt = driftInvalidDt, missionTicks = driftMissionTicks, missionDtSum = driftMissionDtSum, maxMissionDt = driftMaxMissionDt,
            ended = driftEnd, expectedActors = 10, sampleLimitPerActor = DriftSampleLimit,
            completeCoverage = driftEnd == "duration_complete" && driftRows.All(row => row.Invalid == 0 && row.Valid == driftSampleCount),
            provisionalBaselineDriftBudgetMeters = 0.20, acceptance = "diagnostic_only_requires_motion_and_reference_interpretation",
            reference = "station entity global frame composed with custom local frame; no user-frame/cache refresh; Z is geometric, not navmesh-ground height",
            limitations = "20Hz-or-slower game-thread observations, not an atomic native/render cut or continuous-contact proof; no control is applied",
            rows = driftRows.Select(row => new
            {
                slot = row.Index / NavalLabManifest.CrewPerShip, combatantId = row.Combatant,
                kind = row.Index % NavalLabManifest.CrewPerShip == 0 ? "helm" : "oar", ownerLocal = manifest.Controllers[row.Index / NavalLabManifest.CrewPerShip] == ownControllerId,
                shipId = manifest.Ships[row.Index / NavalLabManifest.CrewPerShip], stationKey = row.StationKey,
                pointId = row.First == null ? (int?)null : row.PointId, pointCreatedAtRuntime = row.PointCreatedAtRuntime,
                valid = row.Valid, invalid = row.Invalid, occupancyLoss = row.OccupancyLoss, lastError = row.LastError,
                absoluteHorizontalMeters = DriftDistribution(row.AbsoluteHorizontal), geometricVerticalMeters = DriftDistribution(row.AbsoluteVertical),
                baselineDriftMeters = DriftDistribution(row.BaselineDistance), visualBaselineDriftMeters = DriftDistribution(row.VisualDistance),
                maxHullDisplacementMeters = row.MaximumHullDisplacement, first = row.First, last = row.Last, worstBaselineDrift = row.Worst
            }).ToArray()
        };
    }
}
#endif
