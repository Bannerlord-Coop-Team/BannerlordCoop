using Common.Logging;
using Missions.Messages;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.MountAndBlade;

namespace Missions.Battles;

/// <summary>
/// [Game thread] Moves a registered hull to a new authority. A hull this client takes over is replaced by a freshly
/// simulated hull of the same ship, one step per mission tick (park its agents, replace the hull, board them again), keeping
/// its ship id; a hull it hands away goes back to following samples. A departing hull is parked clear one tick and removed
/// the next, after its departing crew withdrew. An adopted agent is made vanilla's crew of its hull. Every path that
/// spawns a hull itself opens <see cref="SuppressSpawnRegistration"/>, so the spawn event registers no own hull.
/// </summary>
public interface INavalHullAuthority : IDisposable
{
    /// <summary>Whether a hull spawn this client started itself is running.</summary>
    bool IsSpawnRegistrationSuppressed { get; }

    /// <summary>Opens a scope in which spawned hulls are not registered as this client's own; dispose to close it.</summary>
    IDisposable SuppressSpawnRegistration();

    /// <summary>
    /// Gives <paramref name="ship"/> to <paramref name="newAuthority"/>; true when this client now simulates it. A new authority
    /// replaces its copy with a freshly simulated hull, or with <paramref name="inPlace"/> (a capture's captor) adopts the copy
    /// itself; an old authority turns its hull back into a copy.
    /// </summary>
    bool Transfer(NetworkShipInfo ship, string newAuthority, bool inPlace = false);

    /// <summary>Queues replacing a hull this client simulates with a fresh simulated hull of the same ship. Why it cannot, or null.</summary>
    string Replace(Guid shipId);

    /// <summary>
    /// Queues taking hulls out through vanilla's removal: the next <see cref="Tick"/> parks everyone still aboard and the one
    /// after withdraws the <paramref name="crew"/> from the battle, then removes the hulls, as vanilla's removal fades a
    /// removed hull's crew. Parked agents that are not withdrawn then go back where they stood. Their seats must already be free.
    /// </summary>
    void Remove(IReadOnlyList<NetworkShipInfo> ships, DepartingCrew crew);

    /// <summary>
    /// Makes an adopted agent vanilla's crew of its formation's hull. Waits while deployment runs or that hull is being
    /// replaced or removed, since vanilla fades a removed hull's crew.
    /// </summary>
    void EnlistCrew(Agent agent);

    /// <summary>
    /// Whether no removal of <paramref name="owner"/>'s hulls is queued or under way and no swap holds agents parked, so the
    /// owner's party may leave the battle after its hulls did.
    /// </summary>
    bool IsSettledFor(string owner);

    /// <summary>The hulls <see cref="Remove"/> queued and not yet removed; nobody may move onto them, as they go with whoever stands there.</summary>
    IReadOnlyCollection<MissionObject> HullsBeingRemoved { get; }

    /// <summary>The hulls a swap replaces on its next <see cref="Tick"/>; nobody may move onto them, as they go then.</summary>
    IReadOnlyCollection<MissionObject> HullsBeingReplaced { get; }

    bool IsReplacing(Guid shipId);

    /// <summary>The replacement step a hull is at, or null (diagnostics).</summary>
    string SwapPhaseOf(Guid shipId);

    /// <summary>[Mission tick] Advances every queued replacement and removal by one step.</summary>
    void Tick();
}

/// <summary>Which crew of the hulls <see cref="INavalHullAuthority.Remove"/> takes out withdraw from the battle with them.</summary>
public enum DepartingCrew
{
    /// <summary>
    /// Hulls whose owner's mission ended with them: every crew member but this client's main agent withdraws and leaves the
    /// agent registry, as the owner's own removal took its crew out of the battle.
    /// </summary>
    All,

    /// <summary>
    /// Hulls whose owner stays in the battle (a per-ship retreat): nobody withdraws here, since the owner keeps whoever its
    /// removal left alive and the crew it fades reaches peers as routs.
    /// </summary>
    None,
}

/// <inheritdoc cref="INavalHullAuthority"/>
public class NavalHullAuthority : INavalHullAuthority
{
    private static readonly ILogger Logger = LogManager.GetLogger<NavalHullAuthority>();

    private readonly IBattleSession session;
    private readonly ICoopMissionComponent missionComponent;
    private readonly INavalShipEngine engine;
    private readonly IBattleTroopWithdrawal troopWithdrawal;

    private readonly List<HullSwap> hullSwaps = new List<HullSwap>();
    private readonly List<HullRemoval> hullRemovals = new List<HullRemoval>();
    private readonly List<Agent> waitingCrew = new List<Agent>();
    private int spawnScopes;

    public NavalHullAuthority(IBattleSession session, ICoopMissionComponent missionComponent, INavalShipEngine engine,
        IBattleTroopWithdrawal troopWithdrawal)
    {
        this.session = session;
        this.missionComponent = missionComponent;
        this.engine = engine;
        this.troopWithdrawal = troopWithdrawal;
    }

    private INetworkShipRegistry Registry => missionComponent.ShipRegistry;

    public bool IsSpawnRegistrationSuppressed => spawnScopes > 0;

    public IDisposable SuppressSpawnRegistration()
    {
        spawnScopes++;
        return new SpawnScope(this);
    }

    public bool Transfer(NetworkShipInfo ship, string newAuthority, bool inPlace = false)
    {
        bool wasOwn = IsOwn(ship);
        ship.CurrentAuthority = newAuthority;
        bool isOwn = IsOwn(ship);
        if (isOwn && !wasOwn)
            TakeOver(ship, inPlace);
        else if (wasOwn && !isOwn)
            Release(ship);

        return isOwn;
    }

    // A steered copy can simulate as it is (capture); a host change still swaps in a hull spawned the way the host spawned it.
    private void TakeOver(NetworkShipInfo ship, bool inPlace)
    {
        if (inPlace) engine.AdoptHull(ship.Hull);
        else Replace(ship.ShipId);
    }

    public string Replace(Guid shipId)
    {
        if (!Registry.TryGet(shipId, out var ship)) return "No registered hull has that ship id.";
        if (!IsOwn(ship)) return "The hull is not one this client simulates.";
        // A sunk copy is disabled and simulates nothing, so it stays as it is under its new owner.
        if (engine.ReadCondition(ship.Hull)?.SinkingState == BattleShipCondition.Sunk) return "The hull has sunk.";
        if (IsReplacing(shipId)) return "The hull is already being replaced.";

        hullSwaps.Add(new HullSwap(shipId));
        return null;
    }

    public void Remove(IReadOnlyList<NetworkShipInfo> ships, DepartingCrew crew)
    {
        if (ships.Count == 0) return;

        hullRemovals.Add(new HullRemoval(ships.Select(ship => ship.ShipId).ToList(), ships.Select(ship => ship.Hull).ToList(),
            ships.Select(ship => ship.CurrentAuthority).Distinct().ToList(), crew));
    }

    // Enlists at once when it can, as vanilla's off-ship check may run for the agent in this very tick.
    public void EnlistCrew(Agent agent)
    {
        if (!TryEnlist(agent)) waitingCrew.Add(agent);
    }

    // Offers each waiting agent again, taken off the queue first so a throwing enlist is not retried; one that still waits
    // queues anew.
    private void EnlistWaitingCrew()
    {
        foreach (var agent in waitingCrew.ToArray())
        {
            waitingCrew.Remove(agent);
            EnlistCrew(agent);
        }
    }

    // False while deployment runs or the agent's crew hull is going; a dead agent or one without a crew hull needs nothing.
    private bool TryEnlist(Agent agent)
    {
        if (engine.IsDeploymentMode) return false;

        var hull = engine.IsAlive(agent) ? engine.CrewHullOf(agent) : null;
        if (hull == null) return true;
        if (HullsBeingRemoved.Contains(hull) || HullsBeingReplaced.Contains(hull)) return false;

        engine.EnlistCrew(agent, hull);
        return true;
    }

    public bool IsSettledFor(string owner) =>
        !hullRemovals.Any(removal => removal.Owners.Contains(owner)) && !hullSwaps.Any(swap => swap.Agents != null);

    public bool IsReplacing(Guid shipId) => hullSwaps.Any(swap => swap.ShipId == shipId);

    public IReadOnlyCollection<MissionObject> HullsBeingRemoved => hullRemovals.SelectMany(removal => removal.Hulls).ToList();

    // A parking swap still waits on the deployment and a boarding one already has its fresh hull, so only these are going.
    public IReadOnlyCollection<MissionObject> HullsBeingReplaced => hullSwaps
        .Where(swap => swap.Phase == HullSwapPhase.ReplaceHull)
        .Select(swap => Registry.TryGet(swap.ShipId, out var ship) ? ship.Hull : null)
        .Where(hull => hull != null)
        .ToList();

    public string SwapPhaseOf(Guid shipId) => hullSwaps.FirstOrDefault(swap => swap.ShipId == shipId)?.Phase.ToString();

    // A hull handed away mid-swap gets its parked agents back before it turns into a copy.
    private void Release(NetworkShipInfo ship)
    {
        var swap = hullSwaps.FirstOrDefault(pending => pending.ShipId == ship.ShipId);
        if (swap != null)
        {
            hullSwaps.Remove(swap);
            if (swap.Agents != null) engine.BoardHullAgents(ship.Hull, swap.Agents);
        }

        engine.ReleaseHull(ship.Hull);
    }

    // One step per hull per tick, as vanilla removes no hull its kept agents stand on and crews a mid-mission hull the tick after
    // spawning it (Quest5 CallReinforcement, then InitializeReinforcement); waiting crew is enlisted once the steps settled.
    public void Tick()
    {
        foreach (var removal in hullRemovals.ToArray())
            AdvanceHullRemoval(removal);

        foreach (var swap in hullSwaps.ToArray())
            TickHullSwap(swap);

        EnlistWaitingCrew();
    }

    // Advances one swap, which ends it when its ship went away or a step threw.
    private void TickHullSwap(HullSwap swap)
    {
        if (!Registry.TryGet(swap.ShipId, out var ship))
        {
            hullSwaps.Remove(swap);
            if (swap.Agents != null) engine.BoardHullAgents(null, swap.Agents);
            Logger.Error("[NavalSync] Hull swap {ShipId}: the ship went away mid-swap", swap.ShipId);
            return;
        }

        try
        {
            AdvanceHullSwap(swap, ship);
        }
        catch (Exception exception)
        {
            hullSwaps.Remove(swap);
            Logger.Error(exception, "[NavalSync] Hull swap {ShipId} failed in phase {Phase}", swap.ShipId, swap.Phase);
            // Boarding is the step that threw in the last phase, so only an earlier failure hands the parked agents back.
            if (swap.Phase == HullSwapPhase.ReplaceHull && swap.Agents != null) ReturnParkedAgents(swap, ship);
        }
    }

    private void AdvanceHullRemoval(HullRemoval removal)
    {
        var withdrawn = new HashSet<Agent>();
        try
        {
            if (removal.Agents == null)
            {
                ParkHulls(removal);
                Logger.Information("[NavalSync] Departing hull(s) {ShipIds}: parked {Parked} of {Count} agent(s) aboard or crewing them",
                    removal.ShipIds, removal.Agents.Count(moved => moved.IsParked), removal.Agents.Count);
                return;
            }

            hullRemovals.Remove(removal);
            WithdrawCrew(removal, withdrawn);
            foreach (var hull in removal.Hulls)
                engine.RemoveHull(hull);
            Logger.Information(
                "[NavalSync] Departing hull(s) {ShipIds}: withdrew {Withdrawn} crew, then removed the hull(s); {Returning} other agent(s) go back",
                removal.ShipIds, withdrawn.Count, removal.Agents.Count(moved => moved.IsParked && !withdrawn.Contains(moved.Agent)));
        }
        catch (Exception exception)
        {
            hullRemovals.Remove(removal);
            Logger.Error(exception, "[NavalSync] Removing departed hull(s) {ShipIds} failed", removal.ShipIds);
        }

        UnparkTheRest(removal, withdrawn);
    }

    // Records each hull's parked agents as soon as it is done, so a later hull that throws leaves them for UnparkTheRest.
    private void ParkHulls(HullRemoval removal)
    {
        var parked = new List<HullSwapAgent>();
        removal.Agents = parked;
        foreach (var hull in removal.Hulls)
            parked.AddRange(engine.ParkHullAgents(hull));
    }

    // A departed owner's crew is deregistered first, so the despawn of its party later finds nothing left to withdraw.
    private void WithdrawCrew(HullRemoval removal, HashSet<Agent> withdrawn)
    {
        foreach (var moved in removal.Agents.Where(moved => IsWithdrawn(removal, moved)))
        {
            if (!withdrawn.Add(moved.Agent)) continue;

            if (removal.Crew == DepartingCrew.All) troopWithdrawal.Deregister(moved.Agent);
            engine.WithdrawAgent(moved.Agent);
        }
    }

    // Crew is a formation match, so the player's hero can share it; an agent is crew when any departing hull's parking
    // counted it as such.
    private bool IsWithdrawn(HullRemoval removal, HullSwapAgent moved) =>
        removal.Crew == DepartingCrew.All && moved.IsCrew && !engine.IsMainAgent(moved.Agent);

    private void UnparkTheRest(HullRemoval removal, HashSet<Agent> withdrawn)
    {
        if (removal.Agents == null) return;

        var others = removal.Agents.Where(moved => moved.IsParked && !withdrawn.Contains(moved.Agent)).ToList();
        if (others.Count > 0) engine.BoardHullAgents(null, others);
    }

    private void ReturnParkedAgents(HullSwap swap, NetworkShipInfo ship)
    {
        try
        {
            engine.BoardHullAgents(ship.Hull, swap.Agents);
        }
        catch (Exception exception)
        {
            Logger.Error(exception, "[NavalSync] Hull swap {ShipId}: could not hand the parked agents back", swap.ShipId);
        }
    }

    private void AdvanceHullSwap(HullSwap swap, NetworkShipInfo ship)
    {
        switch (swap.Phase)
        {
            case HullSwapPhase.ParkAgents:
                // A hull removed during deployment is pooled and spawning its ship again returns that same copy.
                if (engine.IsDeploymentMode) return;

                swap.Agents = engine.ParkHullAgents(ship.Hull);
                swap.Phase = HullSwapPhase.ReplaceHull;
                Logger.Information("[NavalSync] Hull swap {ShipId}: parked {Count} agent(s) off the hull", ship.ShipId, swap.Agents.Count);
                return;

            case HullSwapPhase.ReplaceHull:
                MissionObject fresh;
                using (SuppressSpawnRegistration())
                {
                    fresh = engine.ReplaceHull(ship.Hull);
                }

                if (fresh == null)
                {
                    hullSwaps.Remove(swap);
                    engine.BoardHullAgents(ship.Hull, swap.Agents);
                    Logger.Error("[NavalSync] Hull swap {ShipId}: the hull could not be replaced; its agents went back aboard", ship.ShipId);
                    return;
                }

                if (!Registry.TryRebindHull(ship.ShipId, fresh))
                    Logger.Error("[NavalSync] Could not rebind hull {ShipId} to its replacement", ship.ShipId);
                swap.Phase = HullSwapPhase.BoardAgents;
                Logger.Information("[NavalSync] Hull swap {ShipId}: replaced the hull with a simulated one", ship.ShipId);
                return;

            case HullSwapPhase.BoardAgents:
                hullSwaps.Remove(swap);
                engine.BoardHullAgents(ship.Hull, swap.Agents);
                Logger.Information("[NavalSync] Hull swap {ShipId}: boarded {Count} agent(s) onto the new hull", ship.ShipId, swap.Agents.Count);
                return;
        }
    }

    private bool IsOwn(NetworkShipInfo ship) => ship.CurrentAuthority == session.OwnControllerId;

    public void Dispose()
    {
        hullSwaps.Clear();
        hullRemovals.Clear();
        waitingCrew.Clear();
    }

    private enum HullSwapPhase
    {
        ParkAgents,
        ReplaceHull,
        BoardAgents,
    }

    private sealed class HullSwap
    {
        public HullSwap(Guid shipId)
        {
            ShipId = shipId;
        }

        public Guid ShipId { get; }
        public HullSwapPhase Phase { get; set; }
        public IReadOnlyList<HullSwapAgent> Agents { get; set; }
    }

    private sealed class HullRemoval
    {
        public HullRemoval(IReadOnlyList<Guid> shipIds, IReadOnlyList<MissionObject> hulls, IReadOnlyList<string> owners,
            DepartingCrew crew)
        {
            ShipIds = shipIds;
            Hulls = hulls;
            Owners = owners;
            Crew = crew;
        }

        public IReadOnlyList<Guid> ShipIds { get; }
        public IReadOnlyList<MissionObject> Hulls { get; }
        /// <summary>The controllers whose hulls these are, as queued.</summary>
        public IReadOnlyList<string> Owners { get; }
        public DepartingCrew Crew { get; }
        /// <summary>Everyone the first step parked off the hulls, filled hull by hull; null before it ran.</summary>
        public IReadOnlyList<HullSwapAgent> Agents { get; set; }
    }

    private sealed class SpawnScope : IDisposable
    {
        private NavalHullAuthority owner;

        public SpawnScope(NavalHullAuthority owner)
        {
            this.owner = owner;
        }

        public void Dispose()
        {
            if (owner == null) return;

            owner.spawnScopes--;
            owner = null;
        }
    }
}
