using E2E.Tests.Util;
using Missions.Battles;
using Missions.Messages;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace E2E.Tests.Environment.MockEngine;

/// <summary>
/// One client's NavalDLC boundary for naval battle E2E tests. Hulls are plain <see cref="FakeHull"/> records keyed by a
/// constructor-skipped <see cref="MissionObject"/>, so the DLC-free replicators run headless over the mission mesh.
/// </summary>
public sealed class FakeNavalShipEngine : INavalShipEngine
{
    public const string HullId = "northern_light_ship";
    public const float FullHitPoints = 100f;
    public const float FullSailHitPoints = 50f;

    private readonly List<FakeHull> hulls = new();

    public IReadOnlyList<MissionObject> Hulls => hulls.Where(hull => !hull.IsRemoved).Select(hull => hull.Hull).ToList();

    public bool IsDeploymentMode { get; set; }

    public bool AreInitialShipsDeployed { get; set; } = true;

    /// <summary>How many of the next foreign hull spawns return null, as when no free formation exists yet.</summary>
    public int FailingForeignSpawns { get; set; }

    public bool ReplaceHullFails { get; set; }

    public bool ReplaceHullThrows { get; set; }

    public int ForeignSpawnAttempts { get; private set; }

    public IReadOnlyList<FakeHull> AllHulls => hulls;

    public IReadOnlyList<FakeHull> Copies => hulls.Where(hull => hull.IsCopy && !hull.IsRemoved).ToList();

    public List<(MissionObject Hull, IReadOnlyList<HullSwapAgent> Agents)> Boardings { get; } = new();

    public List<MissionObject> Releases { get; } = new();

    /// <summary>Creates a simulated hull this client fields, as vanilla's team setup does.</summary>
    public FakeHull Field(BattleSideEnum side, Vec3 position, int formationIndex = 0)
    {
        var hull = new FakeHull(MissionTestObjects.CreateHull(), side, formationIndex, isCopy: false)
        {
            Frame = new MatrixFrame(Mat3.Identity, position),
        };
        hulls.Add(hull);
        return hull;
    }

    public FakeHull Of(MissionObject hull) => hulls.Single(candidate => ReferenceEquals(candidate.Hull, hull));

    public BattleShipSpawnData Describe(MissionObject hull, NetworkShipInfo identity)
    {
        var fake = Of(hull);
        return new BattleShipSpawnData(identity.ShipId, identity.CurrentAuthority, identity.MapEventPartyId, identity.IsNpcParty,
            fake.Side, fake.FormationIndex, NetworkBattleShipSample.FromFrame(fake.Frame), HullId, null, null, null, null,
            fake.HitPoints, fake.SailHitPoints, 1, "");
    }

    public MissionObject SpawnForeignHull(BattleShipSpawnData data, Team team, out Formation formation)
    {
        formation = null!;
        ForeignSpawnAttempts++;
        if (FailingForeignSpawns > 0)
        {
            FailingForeignSpawns--;
            return null!;
        }

        var copy = new FakeHull(MissionTestObjects.CreateHull(), data.Side, data.FormationIndex, isCopy: true)
        {
            Frame = NetworkBattleShipSample.ToFrame(data.Frame),
            HitPoints = data.HitPoints,
            SailHitPoints = data.SailHitPoints,
            Team = team,
            SpawnData = data,
        };
        hulls.Add(copy);
        return copy.Hull;
    }

    public MatrixFrame GetFrame(MissionObject hull) => Of(hull).Frame;

    public void ApplyForeignFrame(MissionObject hull, MatrixFrame frame)
    {
        var fake = Of(hull);
        fake.Frame = frame;
        fake.FrameWrites++;
    }

    public BattleShipInput ReadInput(MissionObject hull) => Of(hull).Input;

    public void ApplyInput(MissionObject hull, BattleShipInput input) => Of(hull).Input = input;

    public bool IsBodyActive(MissionObject hull) => !Of(hull).IsCopy;

    public string ControllerName(MissionObject hull) => Of(hull).IsCopy ? "copy" : "simulated";

    public MissionObject GetSupportHull(Agent agent) => null!;

    public bool TryDescribeStation(UsableMissionObject point, out MissionObject hull, out string stationKey, out int pointIndex)
    {
        foreach (var fake in hulls)
        {
            foreach (var station in fake.Stations)
            {
                if (!ReferenceEquals(station.Value, point)) continue;

                hull = fake.Hull;
                stationKey = station.Key.Key;
                pointIndex = station.Key.Index;
                return true;
            }
        }

        hull = null!;
        stationKey = null!;
        pointIndex = -1;
        return false;
    }

    public UsableMissionObject ResolveStation(MissionObject hull, string stationKey, int pointIndex) =>
        Of(hull).Station(stationKey, pointIndex);

    public void ApplyStationUse(Agent agent, UsableMissionObject point, bool inUse)
    {
        var fake = HullOfStation(point);
        if (inUse) fake.Seats[point] = agent;
        else if (fake.Seats.TryGetValue(point, out var seated) && ReferenceEquals(seated, agent)) fake.Seats.Remove(point);
        fake.StationUses++;
    }

    public bool IsSeated(Agent agent, UsableMissionObject point) =>
        HullOfStation(point).Seats.TryGetValue(point, out var seated) && ReferenceEquals(seated, agent);

    public bool IsAlive(Agent agent) => agent != null && agent.IsActive();

    public void PinToStation(Agent agent, UsableMissionObject point) => HullOfStation(point).Pins++;

    public void ReleaseNpcHull(MissionObject hull)
    {
        Of(hull).IsCopy = true;
        Releases.Add(hull);
    }

    public IReadOnlyList<HullSwapAgent> ParkHullAgents(MissionObject hull)
    {
        var fake = Of(hull);
        var parked = fake.Seats.Values
            .Select(agent => new HullSwapAgent(agent, isCrew: true, isParked: true, Vec3.Zero, Vec3.Zero))
            .ToList();
        fake.Seats.Clear();
        return parked;
    }

    public MissionObject ReplaceHull(MissionObject hull)
    {
        if (ReplaceHullThrows) throw new InvalidOperationException("The fake hull could not be replaced.");
        if (ReplaceHullFails) return null!;

        var old = Of(hull);
        old.IsRemoved = true;
        var fresh = new FakeHull(MissionTestObjects.CreateHull(), old.Side, old.FormationIndex, isCopy: false)
        {
            Frame = old.Frame,
            HitPoints = old.HitPoints,
            SailHitPoints = old.SailHitPoints,
            FireHitPoints = old.FireHitPoints,
            SinkingState = old.SinkingState,
            Team = old.Team,
        };
        hulls.Add(fresh);
        return fresh.Hull;
    }

    public void BoardHullAgents(MissionObject hull, IReadOnlyList<HullSwapAgent> agents) => Boardings.Add((hull, agents));

    public bool HasHelmPilot(MissionObject hull) => false;

    public BattleRopeState[] CaptureRopes(MissionObject hull, Func<MissionObject, Guid> shipIdOf) => Of(hull).Ropes;

    public void ApplyRopes(MissionObject hull, BattleRopeState[] ropes, Func<Guid, MissionObject> hullOf, bool final)
    {
        var fake = Of(hull);
        fake.Ropes = ropes;
        if (final) fake.FinalRopesApplied++;
    }

    public object InspectRopes(IEnumerable<MissionObject> hulls, Func<MissionObject, Guid> shipIdOf) => Array.Empty<object>();

    public void ApplyShipDamage(MissionObject hull, NetworkApplyShipDamage damage, Agent attacker, MissionObject hitter)
    {
        var fake = Of(hull);
        fake.AppliedDamage.Add(damage);
        fake.HitPoints = Math.Max(0f, fake.HitPoints - damage.Damage);
        if (fake.HitPoints <= 0f) fake.SinkingState = BattleShipCondition.Sunk;
    }

    public BattleShipCondition ReadCondition(MissionObject hull)
    {
        var fake = Of(hull);
        return new BattleShipCondition(fake.HitPoints, fake.SailHitPoints, fake.FireHitPoints, null, fake.SinkingState);
    }

    public int ReadSpawnableReserve(MissionObject hull)
    {
        var fake = Of(hull);
        return fake.IsCopy ? 0 : fake.SpawnableReserve;
    }

    public bool IsSinking(MissionObject hull) => Of(hull).SinkingState > 0;

    public bool IsHullRemoved(MissionObject hull)
    {
        var fake = Of(hull);
        return fake.IsRemoved || fake.IsPooled;
    }

    public void ApplyCondition(MissionObject hull, BattleShipCondition condition)
    {
        var fake = Of(hull);
        fake.HitPoints = condition.HitPoints;
        fake.SailHitPoints = condition.SailHitPoints;
        fake.FireHitPoints = condition.FireHitPoints;
        fake.SinkingState = condition.SinkingState;
        fake.AppliedConditions++;
    }

    private FakeHull HullOfStation(UsableMissionObject point) =>
        hulls.Single(fake => fake.Stations.Values.Any(station => ReferenceEquals(station, point)));
}

/// <summary>A fake hull: frame, helm input, damage state, removal flags and the agents seated at its stations.</summary>
public sealed class FakeHull
{
    public FakeHull(MissionObject hull, BattleSideEnum side, int formationIndex, bool isCopy)
    {
        Hull = hull;
        Side = side;
        FormationIndex = formationIndex;
        IsCopy = isCopy;
    }

    public MissionObject Hull { get; }
    public BattleSideEnum Side { get; }
    public int FormationIndex { get; }
    /// <summary>A kinematic copy of another owner's hull, or a hull this client handed away.</summary>
    public bool IsCopy { get; set; }
    public MatrixFrame Frame { get; set; } = MatrixFrame.Identity;
    public BattleShipInput Input { get; set; }
    public float HitPoints { get; set; } = FakeNavalShipEngine.FullHitPoints;
    public float SailHitPoints { get; set; } = FakeNavalShipEngine.FullSailHitPoints;
    public float FireHitPoints { get; set; }
    public int SinkingState { get; set; }
    /// <summary>Troops queued on the hull that can still field, as its NavalShipAgents would hold them.</summary>
    public int SpawnableReserve { get; set; }
    /// <summary>Left the mission: no longer one of the engine's hulls.</summary>
    public bool IsRemoved { get; set; }
    /// <summary>Pooled by deployment: still listed but removed for every station and condition purpose.</summary>
    public bool IsPooled { get; set; }
    public Team? Team { get; set; }
    public BattleShipSpawnData? SpawnData { get; set; }
    public BattleRopeState[] Ropes { get; set; } = Array.Empty<BattleRopeState>();
    public int FrameWrites { get; set; }
    public int FinalRopesApplied { get; set; }
    public int AppliedConditions { get; set; }
    public int StationUses { get; set; }
    public int Pins { get; set; }
    public List<NetworkApplyShipDamage> AppliedDamage { get; } = new();
    public Dictionary<(string Key, int Index), UsableMissionObject> Stations { get; } = new();
    public Dictionary<UsableMissionObject, Agent> Seats { get; } = new(ReferenceEqualityComparer.Instance);

    /// <summary>The usable point at a station's content path, created on first use like a hull's prefab points.</summary>
    public UsableMissionObject Station(string key, int index = 0)
    {
        if (!Stations.TryGetValue((key, index), out var point))
        {
            point = MissionTestObjects.CreateStandingPoint();
            Stations.Add((key, index), point);
        }

        return point;
    }

    public Agent? SeatedAt(string key, int index = 0) => Seats.TryGetValue(Station(key, index), out var agent) ? agent : null;
}
