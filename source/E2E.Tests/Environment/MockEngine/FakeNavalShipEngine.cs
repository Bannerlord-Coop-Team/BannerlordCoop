using E2E.Tests.Util;
using HarmonyLib;
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
    private readonly Dictionary<Agent, FakeHull> decks = new(ReferenceEqualityComparer.Instance);
    private readonly Dictionary<Agent, FakeHull> crews = new(ReferenceEqualityComparer.Instance);

    public IReadOnlyList<MissionObject> Hulls => hulls.Where(hull => !hull.IsRemoved).Select(hull => hull.Hull).ToList();

    public bool IsDeploymentMode { get; set; }

    /// <summary>How many of the next foreign hull spawns return null, as when no free formation exists yet.</summary>
    public int FailingForeignSpawns { get; set; }

    public bool ReplaceHullFails { get; set; }

    public bool ReplaceHullThrows { get; set; }

    public int ForeignSpawnAttempts { get; private set; }

    public IReadOnlyList<FakeHull> AllHulls => hulls;

    public IReadOnlyList<FakeHull> Copies => hulls.Where(hull => hull.IsCopy && !hull.IsRemoved).ToList();

    public List<(MissionObject Hull, IReadOnlyList<HullSwapAgent> Agents)> Boardings { get; } = new();

    public List<MissionObject> Releases { get; } = new();

    /// <summary>
    /// Creates a simulated hull this client fields, as vanilla's team setup does; with a <paramref name="team"/> it takes that
    /// team's formation at <paramref name="formationIndex"/>.
    /// </summary>
    public FakeHull Field(BattleSideEnum side, Vec3 position, int formationIndex = 0, Team? team = null)
    {
        var hull = new FakeHull(MissionTestObjects.CreateHull(), side, formationIndex, isCopy: false)
        {
            Frame = new MatrixFrame(Mat3.Identity, position),
            Team = team,
            Formation = team == null ? null : FormationOf(team, formationIndex),
        };
        hulls.Add(hull);
        return hull;
    }

    // NavalShipsLogic.FindFirstFormationWithoutShip: a copy takes its team's first formation no live hull holds here.
    private int FirstFreeFormation(Team team)
    {
        var taken = hulls.Where(hull => !hull.IsRemoved && hull.Team == team && hull.Formation != null)
            .Select(hull => hull.FormationIndex).ToHashSet();
        return Enumerable.Range(0, 8).First(index => !taken.Contains(index));
    }

    // The headless team's formation shell, carrying its index as a live Formation does.
    private static Formation? FormationOf(Team team, int formationIndex)
    {
        if (!MockTeam.ForShell(team, out var mockTeam)) return null;

        var formation = mockTeam.GetFormation((FormationClass)formationIndex).Shell;
        AccessTools.Field(typeof(Formation), nameof(Formation.FormationIndex)).SetValue(formation, (FormationClass)formationIndex);
        return formation;
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

        int formationIndex = FirstFreeFormation(team);
        var copy = new FakeHull(MissionTestObjects.CreateHull(), data.Side, formationIndex, isCopy: true)
        {
            Frame = NetworkBattleShipSample.ToFrame(data.Frame),
            HitPoints = data.HitPoints,
            SailHitPoints = data.SailHitPoints,
            Team = team,
            Formation = FormationOf(team, formationIndex),
            SpawnData = data,
        };
        hulls.Add(copy);
        formation = copy.Formation!;
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
            Formation = old.Formation,
            CrewCapacity = old.CrewCapacity,
        };
        hulls.Add(fresh);
        return fresh.Hull;
    }

    public void BoardHullAgents(MissionObject hull, IReadOnlyList<HullSwapAgent> agents) => Boardings.Add((hull, agents));

    public bool HasHelmPilot(MissionObject hull) => false;

    /// <summary>Stands <paramref name="agent"/> on <paramref name="deck"/>'s deck, as crew of <paramref name="crewHull"/> when given.</summary>
    public void PlaceAgent(Agent agent, MissionObject deck, MissionObject? crewHull = null)
    {
        decks[agent] = Of(deck);
        if (crewHull != null) crews[agent] = Of(crewHull);
    }

    /// <summary>The hull whose deck the agent stands on, or null.</summary>
    public FakeHull? DeckOf(Agent agent) => decks.TryGetValue(agent, out var deck) ? deck : null;

    public int MovedAgents { get; private set; }

    public IReadOnlyList<Agent> AgentsOnDeck(MissionObject hull)
    {
        var fake = Of(hull);
        return decks.Where(entry => ReferenceEquals(entry.Value, fake) && entry.Key.IsActive()).Select(entry => entry.Key)
            .Concat(fake.Seats.Values)
            .Distinct()
            .ToList();
    }

    public MissionObject CrewHullOf(Agent agent) => crews.TryGetValue(agent, out var crew) ? crew.Hull : null!;

    public MissionObject NearestFriendlyHull(Agent agent, IReadOnlyCollection<MissionObject> excluded)
    {
        var from = DeckOf(agent);
        var side = crews.TryGetValue(agent, out var crew) ? crew.Side : from?.Side;
        return hulls
            .Where(hull => !hull.IsRemoved && hull.SinkingState == 0 && hull.Side == side && !excluded.Contains(hull.Hull))
            .OrderBy(hull => from == null ? 0f : hull.Frame.origin.DistanceSquared(from.Frame.origin))
            .FirstOrDefault()?.Hull!;
    }

    public void MoveAgentToHull(Agent agent, MissionObject hull)
    {
        decks[agent] = Of(hull);
        MovedAgents++;
    }

    public void ReleaseHullStations(MissionObject hull) => Of(hull).Seats.Clear();

    public void RemoveHull(MissionObject hull)
    {
        var fake = Of(hull);
        fake.SeatsAtRemoval = fake.Seats.Count;
        fake.IsRemoved = true;
    }

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

    public BattleSideEnum SideOf(MissionObject hull) => Of(hull).Side;

    public int AddHullReserve(MissionObject hull, IReadOnlyList<IAgentOriginBase> origins)
    {
        var fake = Of(hull);
        if (fake.IsCopy || fake.IsRemoved || fake.SinkingState > 0) return 0;

        int taken = Math.Min(origins.Count, Math.Max(0, fake.CrewCapacity - fake.SpawnableReserve));
        fake.ReservedOrigins.AddRange(origins.Take(taken));
        fake.SpawnableReserve += taken;
        return taken;
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

    public bool TryGetWeapon(MissionObject hull, out MissionObject weapon, out string weaponKey)
    {
        var fake = Of(hull);
        weapon = fake.Weapon!;
        weaponKey = fake.WeaponKey;
        return weapon != null;
    }

    public BattleShipWeaponState ReadWeaponState(MissionObject weapon) => HullOfWeapon(weapon).WeaponState;

    public void ApplyWeaponState(MissionObject weapon, BattleShipWeaponState state, BattleShipWeaponState previous)
    {
        var fake = HullOfWeapon(weapon);
        fake.WeaponState = state;
        fake.AppliedWeaponStates++;
    }

    public bool ReplayWeaponShot(MissionObject weapon, NetworkShipWeaponShot shot, Agent shooter, bool playLaunchEffects)
    {
        HullOfWeapon(weapon).ReplayedShots.Add((shot, shooter, playLaunchEffects));
        return true;
    }

    /// <summary>The weapons this client last claimed to simulate.</summary>
    public IReadOnlyCollection<MissionObject> SimulatedWeapons { get; private set; } = Array.Empty<MissionObject>();

    public void SetSimulatedWeapons(IReadOnlyCollection<MissionObject> own) => SimulatedWeapons = own.ToList();

    public void ClearSimulatedWeapons() => SimulatedWeapons = Array.Empty<MissionObject>();

    private FakeHull HullOfWeapon(MissionObject weapon) => hulls.Single(fake => ReferenceEquals(fake.Weapon, weapon));

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
    /// <summary>How many troops the hull's reserve holds at most.</summary>
    public int CrewCapacity { get; set; } = 40;
    /// <summary>The troops <see cref="FakeNavalShipEngine.AddHullReserve"/> queued on the hull.</summary>
    public List<IAgentOriginBase> ReservedOrigins { get; } = new();
    /// <summary>Left the mission: no longer one of the engine's hulls.</summary>
    public bool IsRemoved { get; set; }
    /// <summary>How many agents still sat at the hull's stations when <see cref="FakeNavalShipEngine.RemoveHull"/> removed it.</summary>
    public int SeatsAtRemoval { get; set; }
    /// <summary>Pooled by deployment: still listed but removed for every station and condition purpose.</summary>
    public bool IsPooled { get; set; }
    public Team? Team { get; set; }
    /// <summary>The local formation the hull is assigned to, which differs per client for the same ship.</summary>
    public Formation? Formation { get; set; }
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
    /// <summary>The hull's ship siege weapon, a constructor-skipped ballista, once <see cref="ArmWeapon"/> mounted it.</summary>
    public RangedSiegeWeapon? Weapon { get; private set; }
    public string WeaponKey { get; private set; } = null!;
    /// <summary>The owner's simulated weapon state, or the last state a copy applied.</summary>
    public BattleShipWeaponState WeaponState { get; set; } = new(0f, 0f, 0, 4);
    public int AppliedWeaponStates { get; set; }
    /// <summary>The owner's shots a copy replayed, with the shooter it used and whether the launch effects played.</summary>
    public List<(NetworkShipWeaponShot Shot, Agent? Shooter, bool LaunchEffects)> ReplayedShots { get; } = new();

    /// <summary>Mounts the hull's ship siege weapon at a content path, as its prefab would.</summary>
    public RangedSiegeWeapon ArmWeapon(string key = "3:ballista_holder/0:ship_ballista")
    {
        Weapon ??= MissionTestObjects.CreateShipWeapon();
        WeaponKey = key;
        return Weapon;
    }

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
