using Common;
using Common.Logging;
using Common.Messaging;
using Missions.Messages;
using Serilog;
using System;
using TaleWorlds.MountAndBlade;

namespace Missions.Battles;

/// <summary>
/// Routes hull damage to the client that simulates the hull, as <see cref="BattleDamageRouter"/> does for agents. The ship
/// damage patches skip vanilla for a hit on another owner's hull and publish it; this sends it over the mesh, and the
/// hull's current authority replays it through the vanilla damage method, so sinking and low-health events stay native.
/// </summary>
public interface IBattleShipDamageRouter : IDisposable
{
    /// <summary>[Game thread] Sent, applied and dropped hits (diagnostics).</summary>
    object Inspect();
}

/// <inheritdoc cref="IBattleShipDamageRouter"/>
public class BattleShipDamageRouter : IBattleShipDamageRouter
{
    private static readonly ILogger Logger = LogManager.GetLogger<BattleShipDamageRouter>();

    private readonly IBattleNetwork network;
    private readonly IMessageBroker messageBroker;
    private readonly IBattleSession session;
    private readonly ICoopMissionComponent missionComponent;
    private readonly INavalShipEngine engine;

    private long sent, applied, dropped;
    private string lastDrop;
    private bool disposed;

    public BattleShipDamageRouter(IBattleNetwork network, IMessageBroker messageBroker, IBattleSession session,
        ICoopMissionComponent missionComponent, INavalShipEngine engine)
    {
        this.network = network;
        this.messageBroker = messageBroker;
        this.session = session;
        this.missionComponent = missionComponent;
        this.engine = engine;

        messageBroker.Subscribe<BattleShipHit>(Handle_BattleShipHit);
        messageBroker.Subscribe<NetworkApplyShipDamage>(Handle_NetworkApplyShipDamage);
    }

    public void Dispose()
    {
        disposed = true;
        messageBroker.Unsubscribe<BattleShipHit>(Handle_BattleShipHit);
        messageBroker.Unsubscribe<NetworkApplyShipDamage>(Handle_NetworkApplyShipDamage);
    }

    private INetworkShipRegistry Registry => missionComponent.ShipRegistry;

    /// <summary>What a damage method does with a hit on a hull, given who simulates the hull and where the hit came from.</summary>
    internal static BattleShipDamageRoute Route(bool isOwnHull, bool isRoutedApply, BattleShipHitOrigin origin)
    {
        if (isRoutedApply) return BattleShipDamageRoute.Vanilla;
        if (origin == BattleShipHitOrigin.ReplayedHit) return BattleShipDamageRoute.Drop;
        if (isOwnHull) return BattleShipDamageRoute.Vanilla;
        return origin == BattleShipHitOrigin.LocalHit ? BattleShipDamageRoute.Send : BattleShipDamageRoute.Drop;
    }

    /// <summary>A puppet's missile or swing is a replay; the client that owns the attacker decides that hit.</summary>
    internal static BattleShipHitOrigin AttackOrigin(bool hasAttacker, bool attackerIsPuppet) =>
        hasAttacker && attackerIsPuppet ? BattleShipHitOrigin.ReplayedHit : BattleShipHitOrigin.LocalHit;

    /// <summary>
    /// A collision belongs to the rammer's owner: queued contact entries exist only for this client's rammers, and a ram
    /// tip's impact counts only when its hull is this client's.
    /// </summary>
    internal static BattleShipHitOrigin CollisionOrigin(bool isQueuedContact, bool hitterIsOwn) =>
        isQueuedContact || hitterIsOwn ? BattleShipHitOrigin.LocalHit : BattleShipHitOrigin.ReplayedHit;

    /// <summary>A contact callback queues the impact on both hulls; only the rammer's owner keeps them.</summary>
    internal static bool KeepsContactCollision(bool rammerIsOwn) => rammerIsOwn;

    // [Game thread] Published synchronously from inside the skipped vanilla damage method.
    private void Handle_BattleShipHit(MessagePayload<BattleShipHit> payload)
    {
        if (disposed) return;

        var hit = payload.What;
        if (!Registry.TryGetByHull(hit.Hull, out var ship))
        {
            Drop("unregistered_hull", Guid.Empty);
            return;
        }

        var damage = new NetworkApplyShipDamage(ship.ShipId, session.OwnControllerId, hit.Kind, hit.Damage, hit.InflictedDamage,
            hit.LocalPoint, AgentIdOf(hit.Attacker), ShipIdOf(hit.Hitter), hit.IsRamDamage, hit.SailIndex, session.HostEpoch);

        // Mid-swap the copy is still in place while this client already holds the hull's authority, so the hit lands here.
        if (ship.CurrentAuthority == session.OwnControllerId)
        {
            Apply(ship, damage);
            return;
        }

        network.SendAll(damage);
        sent++;
    }

    private void Handle_NetworkApplyShipDamage(MessagePayload<NetworkApplyShipDamage> payload)
    {
        var damage = payload.What;
        GameThread.RunSafe(() =>
        {
            if (Mission.Current == null) return;
            ApplyNetworkDamage(damage);
        }, context: nameof(Handle_NetworkApplyShipDamage));
    }

    /// <summary>[Game thread] Applies a routed hit when this client holds the hull's authority, otherwise drops it.</summary>
    internal void ApplyNetworkDamage(NetworkApplyShipDamage damage)
    {
        if (disposed || damage == null) return;

        if (!Registry.TryGet(damage.ShipId, out var ship))
        {
            Drop("unknown_ship", damage.ShipId);
            return;
        }

        var rejection = ValidateDamage(ship, session.OwnControllerId, damage, session.HostEpoch);
        if (rejection != null)
        {
            Drop(rejection, damage.ShipId);
            return;
        }

        Apply(ship, damage);
    }

    /// <summary>Why a routed hit must be dropped, or null to apply it.</summary>
    internal static string ValidateDamage(NetworkShipInfo ship, string ownControllerId, NetworkApplyShipDamage damage, int localEpoch)
    {
        if (ship.CurrentAuthority != ownControllerId) return "not_authority";

        // An AI hull's owner changes with the host epoch; only the owner of the sender's epoch applies, so a hit sent
        // across a migration lands at most once.
        if (ship.IsNpcParty && damage.HostEpoch != 0 && localEpoch != 0 && damage.HostEpoch != localEpoch) return "stale_epoch";
        if (!damage.IsValid) return "invalid_damage";
        return null;
    }

    private void Apply(NetworkShipInfo ship, NetworkApplyShipDamage damage)
    {
        Agent attacker = null;
        if (damage.AttackerAgentId != Guid.Empty && missionComponent.AgentRegistry.TryGetAgentInfo(damage.AttackerAgentId, out var info))
            attacker = info.Agent;

        MissionObject hitter = null;
        if (damage.HitterShipId != Guid.Empty && Registry.TryGet(damage.HitterShipId, out var hitterShip))
            hitter = hitterShip.Hull;

        engine.ApplyShipDamage(ship.Hull, damage, attacker, hitter);
        applied++;
    }

    private void Drop(string reason, Guid shipId)
    {
        dropped++;
        lastDrop = reason;
        Logger.Debug("[NavalDamage] Dropped a hit on hull {ShipId}: {Reason}", shipId, reason);
    }

    private Guid AgentIdOf(Agent agent) =>
        agent != null && missionComponent.AgentRegistry.TryGetAgentInfo(agent, out var info) ? info.AgentId : Guid.Empty;

    private Guid ShipIdOf(MissionObject hull) =>
        hull != null && Registry.TryGetByHull(hull, out var ship) ? ship.ShipId : Guid.Empty;

    public object Inspect() => new
    {
        sent,
        applied,
        dropped,
        lastDrop,
    };
}

/// <summary>Where a hull hit came from, as the ship damage patches see it.</summary>
public enum BattleShipHitOrigin
{
    /// <summary>The hull's own tick (capsizing, burning): only its owner's counts.</summary>
    SelfTick,

    /// <summary>A hit this client decided: its own agent's attack, its own rammer, or a debug command.</summary>
    LocalHit,

    /// <summary>A puppet's replayed attack or another owner's rammer: that owner decides it.</summary>
    ReplayedHit,
}

/// <summary>What a hull damage method does with a hit.</summary>
public enum BattleShipDamageRoute
{
    /// <summary>Run vanilla: an own hull, or a routed hit or condition being applied.</summary>
    Vanilla,

    /// <summary>Skip vanilla and send the hit to the hull's owner.</summary>
    Send,

    /// <summary>Skip vanilla; another client decides this hit.</summary>
    Drop,
}
