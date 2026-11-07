using Common.Messaging;
using Missions.Battles;
using Missions.Messages;
using NavalDLC.Missions.Objects;
using NavalDLC.Missions.ShipActuators;
using System;
using System.Threading;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace Missions.Naval;

/// <summary>
/// Decides inside the vanilla hull damage methods whether this client applies, sends or drops a hit. The damage patches resolve
/// it and open each call's context as a disposable scope, per thread because hull contact callbacks run on physics threads.
/// </summary>
public interface INavalShipDamageGate
{
    /// <summary>Applying a routed hit or an owner's condition: the damage methods run vanilla on any hull until disposed.</summary>
    NavalShipDamageGate.Scope RoutedApply();

    /// <summary>Starts an attack on a hull; a puppet's attack is a replay its owner decides.</summary>
    NavalShipDamageGate.Scope EnterHit(Agent attacker, Vec3 worldPoint);

    /// <summary>Opens the scope of a hull's queued contact impacts, which all came from this client's rammer.</summary>
    NavalShipDamageGate.Scope EnterQueuedContact();

    /// <summary>Starts a hull's contact callback, whose hull is the rammer of any impact it queues.</summary>
    NavalShipDamageGate.Scope EnterContact(MissionShip ship);

    /// <summary>Opens a collision method's scope, marking a routed ram apply.</summary>
    NavalShipDamageGate.Scope EnterCollision(bool isRamDamage);

    /// <summary>Whether a hit method (hull, sails, fire) runs vanilla on <paramref name="ship"/>.</summary>
    bool AllowsHit(MissionShip ship, BattleShipDamageKind kind, float damage, float inflictedDamage, Agent attacker, int sailIndex);

    /// <summary>Whether DealCollisionDamage runs vanilla on <paramref name="ship"/>.</summary>
    bool AllowsCollision(MissionShip ship, MissionShip hitter, bool isRamDamage, Vec3 point, float damage);

    /// <summary>Whether vanilla's ram morale runs: never in a routed ram apply, since each client gave its own agents theirs.</summary>
    bool AllowsRamMorale();

    /// <summary>Whether the running contact callback may queue an impact; only the rammer's owner keeps it.</summary>
    bool KeepsContactCollision();

#if DEBUG
    /// <summary>[Game thread] Deals hull damage the way a ship siege hit does, so the gate applies or routes it like one.</summary>
    void DealLocalHit(MissionShip ship, float damage);
#endif

    /// <summary>[Game thread] Suppressed hits and dropped contact impacts on this client (diagnostics).</summary>
    object Inspect();

    /// <summary>[Game thread] Resets the counters at mission end.</summary>
    void Clear();
}

/// <summary>One per client container; its call contexts are per thread and its counters interlocked.</summary>
public class NavalShipDamageGate : INavalShipDamageGate, IDisposable
{
    private readonly INavalMissionTracker missionTracker;
    private readonly INavalForeignHullRegistry foreignHulls;
    private readonly ThreadLocal<Context> context = new ThreadLocal<Context>();

    private long suppressedHits, droppedContactCollisions;

    // Routes only in this client's coop naval battle, by whether the hull is another owner's copy here.
    public NavalShipDamageGate(INavalMissionTracker missionTracker, INavalForeignHullRegistry foreignHulls)
    {
        this.missionTracker = missionTracker;
        this.foreignHulls = foreignHulls;
    }

    /// <summary>The attack a hull damage method runs under: who decided it, its attacker and its world position.</summary>
    internal readonly struct HitScope
    {
        public HitScope(BattleShipHitOrigin origin, Agent attacker, Vec3 worldPoint)
        {
            IsActive = true;
            Origin = origin;
            Attacker = attacker;
            WorldPoint = worldPoint;
        }

        public bool IsActive { get; }
        public BattleShipHitOrigin Origin { get; }
        public Agent Attacker { get; }
        public Vec3 WorldPoint { get; }
    }

    /// <summary>One thread's open damage scopes; scopes nest, so each restores the whole context it replaced.</summary>
    internal struct Context
    {
        public int RoutedApplyDepth;
        public HitScope Hit;
        public int QueuedContactDepth;
        public MissionShip ContactShip;
        public bool InRoutedRamApply;
    }

    /// <summary>An open damage scope; disposing it restores the thread's context from before it opened. A default one does nothing.</summary>
    public readonly struct Scope : IDisposable
    {
        private readonly NavalShipDamageGate gate;
        private readonly Context previous;

        // Remembers the context the scope replaced on its gate.
        internal Scope(NavalShipDamageGate gate, Context previous)
        {
            this.gate = gate;
            this.previous = previous;
        }

        // Restores the context from before the scope opened.
        public void Dispose()
        {
            if (gate != null) gate.context.Value = previous;
        }
    }

    /// <inheritdoc/>
    public Scope RoutedApply()
    {
        var next = context.Value;
        next.RoutedApplyDepth++;
        return Replace(next);
    }

    /// <inheritdoc/>
    public Scope EnterHit(Agent attacker, Vec3 worldPoint)
    {
        var origin = BattleShipDamageRoutes.AttackOrigin(attacker != null, attacker?.Controller == AgentControllerType.None);
        var next = context.Value;
        next.Hit = new HitScope(origin, attacker, worldPoint);
        return Replace(next);
    }

    /// <inheritdoc/>
    public Scope EnterQueuedContact()
    {
        var next = context.Value;
        next.QueuedContactDepth++;
        return Replace(next);
    }

    /// <inheritdoc/>
    public Scope EnterContact(MissionShip ship)
    {
        var next = context.Value;
        next.ContactShip = ship;
        return Replace(next);
    }

    /// <inheritdoc/>
    public Scope EnterCollision(bool isRamDamage)
    {
        var next = context.Value;
        next.InRoutedRamApply = isRamDamage && next.RoutedApplyDepth > 0;
        return Replace(next);
    }

    /// <inheritdoc/>
    public bool AllowsHit(MissionShip ship, BattleShipDamageKind kind, float damage, float inflictedDamage, Agent attacker,
        int sailIndex)
    {
        var scope = context.Value.Hit;
        var origin = scope.IsActive ? scope.Origin : BattleShipHitOrigin.SelfTick;
        return Allows(ship, origin, kind, damage, inflictedDamage, scope.IsActive ? scope.WorldPoint : Vec3.Invalid,
            attacker ?? scope.Attacker, null, false, sailIndex);
    }

    /// <summary>
    /// A ram it skips still gives this client's agents vanilla's ram morale, so every client that sees the impact applies it once
    /// to its own agents (puppets aren't AI-controlled).
    /// </summary>
    public bool AllowsCollision(MissionShip ship, MissionShip hitter, bool isRamDamage, Vec3 point, float damage)
    {
        var origin = BattleShipDamageRoutes.CollisionOrigin(context.Value.QueuedContactDepth > 0, hitter == null || !foreignHulls.Contains(hitter));
        if (Allows(ship, origin, BattleShipDamageKind.Collision, damage, 0f, point, null, hitter, isRamDamage, -1)) return true;

        if (isRamDamage) ship._moraleInteractionLogic?.OnShipRammed(hitter, ship);
        return false;
    }

    /// <inheritdoc/>
    public bool AllowsRamMorale() => !context.Value.InRoutedRamApply;

    /// <inheritdoc/>
    public bool KeepsContactCollision()
    {
        var rammer = context.Value.ContactShip;
        if (!missionTracker.IsActive || rammer == null) return true;
        if (!foreignHulls.Contains(rammer)) return true;

        Interlocked.Increment(ref droppedContactCollisions);
        return false;
    }

    // Swaps in this thread's next context, returning the scope that restores the current one.
    private Scope Replace(Context next)
    {
        var previous = context.Value;
        context.Value = next;
        return new Scope(this, previous);
    }

    // Runs vanilla when the route says so; otherwise skips it and publishes a hit to send to the hull's authority.
    private bool Allows(MissionShip ship, BattleShipHitOrigin origin, BattleShipDamageKind kind, float damage,
        float inflictedDamage, Vec3 worldPoint, Agent attacker, MissionShip hitter, bool isRamDamage, int sailIndex)
    {
        if (!missionTracker.IsActive) return true;

        var route = BattleShipDamageRoutes.Route(!foreignHulls.Contains(ship), context.Value.RoutedApplyDepth > 0, origin);
#if DEBUG
        if (kind == BattleShipDamageKind.Collision) NavalContactProbe.CountCollisionRoute(route, isRamDamage);
#endif
        if (route == BattleShipDamageRoute.Vanilla) return true;

        Interlocked.Increment(ref suppressedHits);
        if (route == BattleShipDamageRoute.Send)
        {
            var localPoint = worldPoint.IsValid ? ship.GlobalFrame.TransformToLocal(worldPoint) : Vec3.Zero;
            MessageBroker.Instance.Publish(ship, new BattleShipHit(ship, kind, damage, inflictedDamage, localPoint, attacker, hitter,
                isRamDamage, sailIndex));
        }

        return false;
    }

#if DEBUG
    /// <inheritdoc/>
    public void DealLocalHit(MissionShip ship, float damage)
    {
        using (EnterHit(null, ship.GlobalFrame.origin))
            ship.DealDamage(damage, null, out _, out _, out _, out _);
    }
#endif

    /// <summary>The sail's index in the hull's sails, or -1.</summary>
    internal static int SailIndexOf(MissionShip ship, MissionSail sail)
    {
        if (sail == null) return -1;
        var sails = ship.Sails;
        for (int index = 0; index < sails.Count; index++)
        {
            if (sails[index] == sail) return index;
        }

        return -1;
    }

    /// <inheritdoc/>
    public object Inspect() => new
    {
        suppressedHits = Interlocked.Read(ref suppressedHits),
        droppedContactCollisions = Interlocked.Read(ref droppedContactCollisions),
    };

    /// <inheritdoc/>
    public void Clear()
    {
        Interlocked.Exchange(ref suppressedHits, 0);
        Interlocked.Exchange(ref droppedContactCollisions, 0);
    }

    // Frees the per-thread contexts with the client container.
    public void Dispose() => context.Dispose();
}
