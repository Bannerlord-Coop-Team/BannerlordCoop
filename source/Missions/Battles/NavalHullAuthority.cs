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
/// its ship id; a hull it hands away goes back to following samples. Every path that spawns a hull itself opens
/// <see cref="SuppressSpawnRegistration"/>, so the spawn event registers no own hull.
/// </summary>
public interface INavalHullAuthority : IDisposable
{
    /// <summary>Whether a hull spawn this client started itself is running.</summary>
    bool IsSpawnRegistrationSuppressed { get; }

    /// <summary>Opens a scope in which spawned hulls are not registered as this client's own; dispose to close it.</summary>
    IDisposable SuppressSpawnRegistration();

    /// <summary>Gives <paramref name="ship"/> to <paramref name="newAuthority"/>; true when this client now simulates it.</summary>
    bool Transfer(NetworkShipInfo ship, string newAuthority);

    /// <summary>Queues replacing a hull this client simulates with a fresh simulated hull of the same ship. Why it cannot, or null.</summary>
    string Replace(Guid shipId);

    /// <summary>
    /// Takes a hull out of the mission through vanilla's removal, ending any replacement it was in; agents parked for that
    /// replacement go back where they stood. Its seats and deck must already be clear.
    /// </summary>
    void Remove(NetworkShipInfo ship);

    bool IsReplacing(Guid shipId);

    /// <summary>The replacement step a hull is at, or null (diagnostics).</summary>
    string SwapPhaseOf(Guid shipId);

    /// <summary>[Mission tick] Advances every queued replacement by one step.</summary>
    void Tick();
}

/// <inheritdoc cref="INavalHullAuthority"/>
public class NavalHullAuthority : INavalHullAuthority
{
    private static readonly ILogger Logger = LogManager.GetLogger<NavalHullAuthority>();

    private readonly IBattleSession session;
    private readonly ICoopMissionComponent missionComponent;
    private readonly INavalShipEngine engine;

    private readonly List<HullSwap> hullSwaps = new List<HullSwap>();
    private int spawnScopes;

    public NavalHullAuthority(IBattleSession session, ICoopMissionComponent missionComponent, INavalShipEngine engine)
    {
        this.session = session;
        this.missionComponent = missionComponent;
        this.engine = engine;
    }

    private INetworkShipRegistry Registry => missionComponent.ShipRegistry;

    public bool IsSpawnRegistrationSuppressed => spawnScopes > 0;

    public IDisposable SuppressSpawnRegistration()
    {
        spawnScopes++;
        return new SpawnScope(this);
    }

    public bool Transfer(NetworkShipInfo ship, string newAuthority)
    {
        bool wasOwn = IsOwn(ship);
        ship.CurrentAuthority = newAuthority;
        bool isOwn = IsOwn(ship);
        if (isOwn && !wasOwn)
            Replace(ship.ShipId);
        else if (wasOwn && !isOwn)
            Release(ship);

        return isOwn;
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

    public void Remove(NetworkShipInfo ship)
    {
        var swap = hullSwaps.FirstOrDefault(pending => pending.ShipId == ship.ShipId);
        if (swap != null)
        {
            hullSwaps.Remove(swap);
            if (swap.Agents != null) engine.BoardHullAgents(null, swap.Agents);
        }

        engine.RemoveHull(ship.Hull);
    }

    public bool IsReplacing(Guid shipId) => hullSwaps.Any(swap => swap.ShipId == shipId);

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

        engine.ReleaseNpcHull(ship.Hull);
    }

    // One step per hull per tick: vanilla removes no hull its kept agents stand on, and crews a mid-mission hull the tick
    // after spawning it (Quest5 CallReinforcement, then InitializeReinforcement).
    public void Tick()
    {
        foreach (var swap in hullSwaps.ToArray())
        {
            if (!Registry.TryGet(swap.ShipId, out var ship))
            {
                hullSwaps.Remove(swap);
                if (swap.Agents != null) engine.BoardHullAgents(null, swap.Agents);
                Logger.Error("[NavalSync] Hull swap {ShipId}: the ship went away mid-swap", swap.ShipId);
                continue;
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
                // A hull removed during deployment is pooled and spawning its ship again returns that same kinematic copy.
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

    public void Dispose() => hullSwaps.Clear();

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
