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
/// It also checks the removal invariant on every hull the code under test removes or replaces; see
/// <see cref="AssertRemovalInvariant"/>.
/// </summary>
public sealed class FakeNavalShipEngine : INavalShipEngine, INavalShipCaptureEngine
{
    public const string HullId = "northern_light_ship";
    public const float FullHitPoints = 100f;
    public const float FullSailHitPoints = 50f;

    private readonly List<FakeHull> hulls = new();
    private readonly Dictionary<Agent, FakeHull> decks = new(ReferenceEqualityComparer.Instance);
    private readonly Dictionary<Agent, FakeHull> crews = new(ReferenceEqualityComparer.Instance);
    // Parking lifts an agent clear with its physics force-closed (NavalShipEngine.ParkHullAgents), so one entry is both.
    private readonly Dictionary<Agent, FakeHull> parkedFrom = new(ReferenceEqualityComparer.Instance);
    // Every agent any parking lifted, kept after it is put back, so a park then unpark still shows.
    private readonly HashSet<Agent> everParked = new(ReferenceEqualityComparer.Instance);
    // The free ship assignment slots lent to hulls holding none, as NavalShipEngine.RemoveThroughLentAssignment lends them.
    private readonly List<FakeHull> lentSlots = new();
    private readonly List<string> removalViolations = new();

    public FakeNavalShipEngine(string clientId)
    {
        ClientId = clientId;
    }

    /// <summary>The client this engine belongs to, named in removal invariant failures.</summary>
    public string ClientId { get; }

    /// <summary>Names an agent in removal invariant failures; its mission index when unset or null.</summary>
    public Func<Agent, string?>? AgentName { get; set; }

    /// <summary>The ship id the registry holds a hull under, or null; names hulls in removal invariant failures.</summary>
    public Func<MissionObject, Guid?>? RegisteredShipId { get; set; }

    public IReadOnlyList<MissionObject> Hulls => hulls.Where(hull => !hull.IsRemoved).Select(hull => hull.Hull).ToList();

    public bool IsDeploymentMode { get; set; }

    /// <summary>How many of the next foreign hull spawns return null, as when no free formation exists yet.</summary>
    public int FailingForeignSpawns { get; set; }

    public bool ReplaceHullFails { get; set; }

    public bool ReplaceHullThrows { get; set; }

    public int ForeignSpawnAttempts { get; private set; }

    /// <summary>Copies spawn with their prefab's ship weapon, as every hull of a ballista ship does.</summary>
    public bool ArmsCopies { get; set; }

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
            identity.IsOrphaned ? identity.OrphanedSide : fake.Side, fake.FormationIndex,
            NetworkBattleShipSample.FromFrame(fake.Frame), HullId, null, null, null, null, fake.HitPoints, fake.SailHitPoints, 1, "",
            identity.OriginalOwner, fake.CampaignShipId, identity.CaptorControllerId, identity.IsOrphaned, identity.CaptureSequence,
            identity.Capture?.CaptorShipId ?? Guid.Empty, identity.Capture?.FromSide ?? BattleSideEnum.None);
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
            CampaignShipId = data.CampaignShipId,
        };
        if (ArmsCopies) copy.ArmWeapon();
        hulls.Add(copy);
        formation = copy.Formation!;
        return copy.Hull;
    }

    public MatrixFrame GetFrame(MissionObject hull) => Of(hull).Frame;

    // Stands in for the steered copy reaching the owner's sample at once.
    public void ApplyForeignSample(MissionObject hull, MatrixFrame frame, BattleShipMotion motion)
    {
        var fake = Of(hull);
        fake.Frame = frame;
        fake.Motion = motion;
    }

    public BattleShipMotion ReadMotion(MissionObject hull) => Of(hull).Motion;

    public void TickCopies(float fixedDt)
    {
    }

    public void SnapCopies()
    {
    }

    public void ForgetHull(MissionObject hull)
    {
    }

    public BattleShipInput ReadInput(MissionObject hull) => Of(hull).Input;

    public void ApplyInput(MissionObject hull, BattleShipInput input) => Of(hull).Input = input;

    // A copy here stands in for one whose body simulates.
    public string BodyMode(MissionObject hull) => Of(hull).IsCopy ? "steered" : "own";

    public string ControllerName(MissionObject hull) => Of(hull).IsCopy ? "copy" : "simulated";

    public MissionObject GetSupportHull(Agent agent) => null!;

    // Fake hulls have no physics box, so no deck pose is refused for its distance.
    public bool TryGetFootprint(MissionObject hull, out Vec2 min, out Vec2 max)
    {
        min = max = Vec2.Zero;
        return false;
    }

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

    public bool IsMainAgent(Agent agent) => agent != null && agent == Mission.Current?.MainAgent;

    public void PinToStation(Agent agent, UsableMissionObject point) => HullOfStation(point).Pins++;

    public void ReleaseHull(MissionObject hull)
    {
        Of(hull).IsCopy = true;
        Releases.Add(hull);
    }

    /// <summary>The copies this client took over in place (capture), in order.</summary>
    public List<MissionObject> Adoptions { get; } = new();

    // Stands in for dropping the copy's steering: the same hull simulates here from now on.
    public void AdoptHull(MissionObject hull)
    {
        Of(hull).IsCopy = false;
        Adoptions.Add(hull);
    }

    /// <summary>Each capture swap this client replayed, as (prize, captor hull).</summary>
    public List<(MissionObject Prize, MissionObject CaptorHull)> Captures { get; } = new();

    // Vanilla's swap: the prize takes the captor hull's team, side and formation, the captor's crew crews the prize, and the
    // captor hull is left with none. Vanilla also points the old owner's crew record of the prize at the orphan
    // (NavalShipAgents.OnShipCaptured), so those agents stay tracked on the orphan while on another team.
    public void ApplyCapture(MissionObject prize, MissionObject captorHull)
    {
        var fake = Of(prize);
        var captor = Of(captorHull);
        Captures.Add((prize, captorHull));

        var oldCrew = crews.Where(entry => ReferenceEquals(entry.Value, fake)).Select(entry => entry.Key).ToList();
        foreach (var agent in crews.Where(entry => ReferenceEquals(entry.Value, captor)).Select(entry => entry.Key).ToList())
            crews[agent] = fake;
        foreach (var agent in oldCrew)
            crews[agent] = captor;

        fake.Team = captor.Team;
        fake.Formation = captor.Formation;
        fake.Side = captor.Side;
        fake.FormationIndex = captor.FormationIndex;
        // NavalShipsLogic.OnShipCaptured points the captor formation's assignment at the prize, so the orphan holds none.
        fake.HoldsAssignment = true;
        Orphan(captor);
    }

    public void OrphanHull(MissionObject hull) => Orphan(Of(hull));

    /// <summary>The reason this client's swap of the next capture can't run, standing for a missing hull or formation; null lets it.</summary>
    public string CaptureBlocker { get; set; }

    // The live engine checks what vanilla's swap dereferences; the fake refuses only when a test sets a reason.
    public string CheckCapture(MissionObject prize, MissionObject captorHull) => CaptureBlocker;

    // A hull with no formation has no team either, so its side reads as none, as MissionShip.BattleSide does.
    private static void Orphan(FakeHull fake)
    {
        fake.Team = null;
        fake.Formation = null;
        fake.Side = BattleSideEnum.None;
        fake.IsOrphaned = true;
        fake.HoldsAssignment = false;
    }

    public bool IsVacantForCapture(MissionObject hull) => Of(hull).IsVacant;

    public bool CanBeTakenOver(MissionObject hull) => !Of(hull).CannotBeTakenOver;

    // Like the live engine: everyone aboard is parked, and the hull's crew elsewhere is listed without being moved.
    public IReadOnlyList<HullSwapAgent> ParkHullAgents(MissionObject hull)
    {
        var fake = Of(hull);
        fake.Parkings++;
        var parked = fake.Seats.Values
            .Select(agent => new HullSwapAgent(agent, isCrew: true, isParked: true, Vec3.Zero, Vec3.Zero))
            .ToList();
        fake.Seats.Clear();
        foreach (var agent in decks.Where(entry => ReferenceEquals(entry.Value, fake)).Select(entry => entry.Key).ToList())
        {
            bool isCrew = IsCrewOf(agent, fake);
            parked.Add(new HullSwapAgent(agent, isCrew, isParked: true, Vec3.Zero, Vec3.Zero));
            decks.Remove(agent);
        }

        foreach (var moved in parked)
        {
            parkedFrom[moved.Agent] = fake;
            everParked.Add(moved.Agent);
        }
        foreach (var agent in CrewOf(fake).Where(agent => parked.All(moved => moved.Agent != agent)))
            parked.Add(new HullSwapAgent(agent, isCrew: true, isParked: false, Vec3.Zero, Vec3.Zero));

        return parked;
    }

    /// <summary>The active agents that crew <paramref name="fake"/>.</summary>
    private IEnumerable<Agent> CrewOf(FakeHull fake) =>
        crews.Where(entry => entry.Key.IsActive() && IsCrewOf(entry.Key, fake)).Select(entry => entry.Key).ToList();

    // As NavalShipEngine.IsHullCrew: an orphan has no team, so the old owner's crew vanilla tracks on it never counts. A test
    // agent without a team stands for crew whose team this harness does not model.
    private bool IsCrewOf(Agent agent, FakeHull fake) =>
        crews.TryGetValue(agent, out var crew) && ReferenceEquals(crew, fake)
        && !(fake.IsOrphaned && agent.Team != null);

    /// <summary>The agents a hull's parking lifted clear that nothing has put back or withdrawn yet.</summary>
    public IReadOnlyCollection<Agent> ParkedAgents => parkedFrom.Keys;

    /// <summary>Every agent a parking lifted during the test, whether or not it was put back since.</summary>
    public IReadOnlyCollection<Agent> AgentsEverParked => everParked;

    /// <summary>Lent ship assignment slots still holding a hull; vanilla's removal clears each one again.</summary>
    public int LentSlotsHeld => lentSlots.Count;

    public MissionObject ReplaceHull(MissionObject hull)
    {
        if (ReplaceHullThrows) throw new InvalidOperationException("The fake hull could not be replaced.");
        if (ReplaceHullFails) return null!;

        var old = Of(hull);
        CheckClearForRemoval(old, "replaced");
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
            CampaignShipId = old.CampaignShipId,
        };
        hulls.Add(fresh);
        // The replacement takes the old hull's formation, so its crew and the agents parked for boarding it carry over.
        foreach (var agent in crews.Where(entry => entry.Value == old).Select(entry => entry.Key).ToList())
            crews[agent] = fresh;
        foreach (var agent in parkedFrom.Where(entry => entry.Value == old).Select(entry => entry.Key).ToList())
            parkedFrom[agent] = fresh;
        return fresh.Hull;
    }

    public void BoardHullAgents(MissionObject hull, IReadOnlyList<HullSwapAgent> agents)
    {
        Boardings.Add((hull, agents));
        var boarded = hull == null ? null : Of(hull);
        foreach (var moved in agents.Where(moved => moved.IsParked))
        {
            // A swap's re-attribution must not leave an agent counted both parked for the fresh hull and aboard it.
            if (boarded != null && parkedFrom.ContainsKey(moved.Agent) && moved.Agent.IsActive()
                && (DeckOf(moved.Agent) == boarded || boarded.Seats.Values.Contains(moved.Agent)))
                removalViolations.Add($"{NameOf(moved.Agent)} was both parked for and aboard {Describe(boarded)} when boarded");
            parkedFrom.Remove(moved.Agent);
        }

        if (boarded == null || boarded.IsRemoved) return;

        foreach (var agent in parkedFrom.Where(entry => entry.Value == boarded && entry.Key.IsActive()).Select(entry => entry.Key))
            removalViolations.Add($"{NameOf(agent)} stayed parked for {Describe(boarded)} after its agents boarded it");
    }

    public bool HasHelmPilot(MissionObject hull) => false;

    /// <summary>Stands <paramref name="agent"/> on <paramref name="deck"/>'s deck, as crew of <paramref name="crewHull"/> when given.</summary>
    public void PlaceAgent(Agent agent, MissionObject deck, MissionObject? crewHull = null)
    {
        decks[agent] = Of(deck);
        if (crewHull != null) crews[agent] = Of(crewHull);
    }

    /// <summary>Makes <paramref name="agent"/> crew of <paramref name="crewHull"/> without standing it on a deck, as a swimmer.</summary>
    public void AssignCrew(Agent agent, MissionObject crewHull) => crews[agent] = Of(crewHull);

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

    public MissionObject CrewHullOf(Agent agent) => crews.TryGetValue(agent, out var crew) && IsCrewOf(agent, crew) ? crew.Hull : null!;

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
        CheckClearForRemoval(fake, "removed");
        fake.CrewActiveAtRemoval = CrewOf(fake).Count();
        fake.ParkingsBeforeRemoval = fake.Parkings;
        // As the live engine: a hull holding no ship assignment (a capture's orphan) is lent a free slot for the removal.
        if (!fake.HoldsAssignment) lentSlots.Add(fake);
        RemoveThroughAssignment(fake);
    }

    // Vanilla's NavalShipsLogic.RemoveShip: it finds a hull only through the assignment slot holding it, its own or a lent
    // one, and RemoveShipAux clears that slot; a hull no slot holds stays where it is.
    private void RemoveThroughAssignment(FakeHull fake)
    {
        bool throughLentSlot = lentSlots.Contains(fake);
        if (!fake.HoldsAssignment && !throughLentSlot) return;

        fake.IsRemoved = true;
        fake.HoldsAssignment = false;
        fake.RemovedThroughLentSlot = throughLentSlot;
        lentSlots.Remove(fake);
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
        fake.AppliedHitters.Add(hitter);
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

    public string CampaignShipIdOf(MissionObject hull) => Of(hull).CampaignShipId!;

    public float ReadMitigatedDamage(MissionObject hull) => Of(hull).MitigatedDamage;

    // The live engine fades the agent out of the mission, which the mission fixture's FadeOut patch mirrors.
    public void WithdrawAgent(Agent agent)
    {
        if (!agent.IsActive()) return;

        decks.Remove(agent);
        parkedFrom.Remove(agent);
        agent.FadeOut(true, true);
        Withdrawn.Add(agent);
    }

    /// <summary>
    /// Test stand-in for vanilla removing a hull of the owner's own (NavalShipsLogic.OnEndMission, ShipRetreatLogic), which
    /// fades its tracked crew inside the removal, so the removal invariant does not apply to it. Not on the engine interface.
    /// </summary>
    public void VanillaOwnerRemoveShipForTest(MissionObject hull)
    {
        var fake = Of(hull);
        Assert.False(fake.IsCopy, $"{Describe(fake)} is a copy; only an owner's own hull is exempt from the removal invariant");
        // NavalShipsLogic.RemoveShip acts only through the assignment holding the hull, and a capture's orphan holds none.
        RemoveThroughAssignment(fake);
        if (!fake.IsRemoved) return;

        fake.RemovedByVanillaForTest = true;
    }

    /// <summary>
    /// The removal invariant the R33 and R40 crashes broke: when the code under test removes or replaces a hull, nobody stands
    /// on its deck or sits at its stations (checked inside <see cref="RemoveHull"/> and <see cref="ReplaceHull"/>), and
    /// afterwards nobody is left parked (force-closed) from a removed hull or parked while crew of one, and nobody stands on
    /// one. Fails with every violation recorded since the last call.
    /// </summary>
    public void AssertRemovalInvariant()
    {
        foreach (var fake in hulls.Where(fake => !fake.IsRemoved))
            RefreshShipId(fake);
        CheckRemovedHullsLeftNobody();
        if (removalViolations.Count == 0) return;

        var violations = removalViolations.Distinct().ToList();
        removalViolations.Clear();
        HasFailedRemovalInvariant = true;
        Assert.Fail($"Naval removal invariant broken on {ClientId}:\n" + string.Join("\n", violations));
    }

    /// <summary>Whether <see cref="AssertRemovalInvariant"/> already failed the scenario.</summary>
    public bool HasFailedRemovalInvariant { get; private set; }

    private void CheckClearForRemoval(FakeHull fake, string removal)
    {
        RefreshShipId(fake);
        foreach (var seat in fake.Seats)
        {
            var station = fake.Stations.First(entry => ReferenceEquals(entry.Value, seat.Key)).Key;
            removalViolations.Add($"{Describe(fake)} was {removal} while {NameOf(seat.Value)} sat at its station {station.Key}/{station.Index}");
        }

        foreach (var agent in decks.Where(entry => entry.Value == fake && entry.Key.IsActive()).Select(entry => entry.Key))
            removalViolations.Add($"{Describe(fake)} was {removal} while {NameOf(agent)} stood on its deck");
    }

    private void CheckRemovedHullsLeftNobody()
    {
        foreach (var parked in parkedFrom.Where(entry => entry.Key.IsActive()))
        {
            if (IsInvariantRemoved(parked.Value))
                removalViolations.Add($"{NameOf(parked.Key)} is still parked (physics force-closed) from removed {Describe(parked.Value)}");
            else if (crews.TryGetValue(parked.Key, out var crew) && IsInvariantRemoved(crew))
                removalViolations.Add($"{NameOf(parked.Key)} is parked from {Describe(parked.Value)} while crew of removed {Describe(crew)}");
        }

        foreach (var deck in decks.Where(entry => IsInvariantRemoved(entry.Value) && entry.Key.IsActive()))
            removalViolations.Add($"{NameOf(deck.Key)} still stands on removed {Describe(deck.Value)}");
    }

    // The registry drops a removed hull, so the hull keeps the last id it was registered under.
    private void RefreshShipId(FakeHull fake)
    {
        if (RegisteredShipId?.Invoke(fake.Hull) is Guid shipId) fake.ShipId = shipId;
    }

    private static bool IsInvariantRemoved(FakeHull fake) => fake.IsRemoved && !fake.RemovedByVanillaForTest;

    private string NameOf(Agent agent) => AgentName?.Invoke(agent) ?? $"agent #{agent.Index}";

    private static string Describe(FakeHull fake) =>
        $"hull {(fake.ShipId == Guid.Empty ? "(unregistered)" : fake.ShipId.ToString())} ({(fake.IsCopy ? "copy" : "simulated")}, "
        + $"{fake.Side} formation {fake.FormationIndex})";

    /// <summary>The agents <see cref="WithdrawAgent"/> took out of the battle.</summary>
    public List<Agent> Withdrawn { get; } = new();

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
    /// <summary>The side of the hull's team; none once a capture left it without one.</summary>
    public BattleSideEnum Side { get; set; }
    public int FormationIndex { get; set; }
    /// <summary>Vanilla's capture vacancy on this client's view of the hull: crew all swimming or gone, nothing queued.</summary>
    public bool IsVacant { get; set; }
    /// <summary>A storyline hull vanilla never lets be captured.</summary>
    public bool CannotBeTakenOver { get; set; }
    /// <summary>A capture took its formation for the prize.</summary>
    public bool IsOrphaned { get; set; }
    /// <summary>A copy of another owner's hull, or a hull this client handed away.</summary>
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
    /// <summary>The ship id the registry last held the hull under, read at each removal invariant check; empty until then.</summary>
    public Guid ShipId { get; set; }
    /// <summary>Left the mission: no longer one of the engine's hulls.</summary>
    public bool IsRemoved { get; set; }
    /// <summary>Removed by <see cref="FakeNavalShipEngine.VanillaOwnerRemoveShipForTest"/>, which the removal invariant exempts.</summary>
    public bool RemovedByVanillaForTest { get; set; }
    /// <summary>Holds a ship assignment slot, as every spawned hull does until a capture orphans it.</summary>
    public bool HoldsAssignment { get; set; } = true;
    /// <summary>Removed through a lent slot, since it held no ship assignment of its own (a capture's orphan).</summary>
    public bool RemovedThroughLentSlot { get; set; }
    /// <summary>How many of the hull's crew were still in the battle when it was removed.</summary>
    public int CrewActiveAtRemoval { get; set; }
    /// <summary>How many times <see cref="FakeNavalShipEngine.ParkHullAgents"/> parked the hull's agents.</summary>
    public int Parkings { get; set; }
    /// <summary>The value of <see cref="Parkings"/> when the hull was removed.</summary>
    public int ParkingsBeforeRemoval { get; set; }
    /// <summary>Pooled by deployment: still listed but removed for every station and condition purpose.</summary>
    public bool IsPooled { get; set; }
    public Team? Team { get; set; }
    /// <summary>The local formation the hull is assigned to, which differs per client for the same ship.</summary>
    public Formation? Formation { get; set; }
    public BattleShipSpawnData? SpawnData { get; set; }
    /// <summary>The registry id of the campaign ship the hull stands for, or null.</summary>
    public string? CampaignShipId { get; set; }
    /// <summary>The damage the ship upgrades mitigated on hits this client applied, as the snapshot XP patch sums it.</summary>
    public float MitigatedDamage { get; set; }
    public BattleRopeState[] Ropes { get; set; } = Array.Empty<BattleRopeState>();
    /// <summary>An own hull's body motion its samples send, or the motion a copy's latest sample carried.</summary>
    public BattleShipMotion Motion { get; set; }
    public int FinalRopesApplied { get; set; }
    public int AppliedConditions { get; set; }
    public int StationUses { get; set; }
    public int Pins { get; set; }
    public List<NetworkApplyShipDamage> AppliedDamage { get; } = new();
    /// <summary>The hitter hull each routed hit resolved to here, null when it was not known.</summary>
    public List<MissionObject?> AppliedHitters { get; } = new();
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
