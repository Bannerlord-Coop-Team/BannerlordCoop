using Common;
using Common.Logging;
using Common.Messaging;
using Missions.Messages;
using Serilog;
using System;
using System.Collections.Generic;
using TaleWorlds.MountAndBlade;

namespace Missions.Battles;

/// <summary>
/// Sends a hit on another owner's hull to that hull's current authority only, which replays it through the vanilla damage
/// method, so each hit applies at most once across a host change or a capture.
/// </summary>
public interface IBattleShipDamageRouter : IDisposable
{
    /// <summary>[Game thread] Sent, deferred and applied hits, and dropped ones by reason (diagnostics).</summary>
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
    private readonly IAppliedHostEpoch hostEpoch;
    private readonly IBattleShipArrivals arrivals;

    private readonly Dictionary<string, long> drops = new Dictionary<string, long>();
    private long sent, deferred, applied;
    private bool disposed;

    public BattleShipDamageRouter(IBattleNetwork network, IMessageBroker messageBroker, IBattleSession session,
        ICoopMissionComponent missionComponent, INavalShipEngine engine, IAppliedHostEpoch hostEpoch, IBattleShipArrivals arrivals)
    {
        this.network = network;
        this.messageBroker = messageBroker;
        this.session = session;
        this.missionComponent = missionComponent;
        this.engine = engine;
        this.hostEpoch = hostEpoch;
        this.arrivals = arrivals;

        messageBroker.Subscribe<BattleShipHit>(Handle_BattleShipHit);
        messageBroker.Subscribe<NetworkApplyShipDamage>(Handle_NetworkApplyShipDamage);
    }

    // Stops routing as the battle ends; hits still waiting in the arrivals are cleared with them.
    public void Dispose()
    {
        disposed = true;
        messageBroker.Unsubscribe<BattleShipHit>(Handle_BattleShipHit);
        messageBroker.Unsubscribe<NetworkApplyShipDamage>(Handle_NetworkApplyShipDamage);
    }

    private INetworkShipRegistry Registry => missionComponent.ShipRegistry;

    // A host change the session already read, whose AI hull move still waits in the arrivals queue.
    private bool IsHostChangeQueued => session.HostEpoch != hostEpoch.AppliedHostEpoch;

    // [Game thread] Published synchronously from inside the skipped vanilla damage method.
    private void Handle_BattleShipHit(MessagePayload<BattleShipHit> payload)
    {
        if (disposed) return;

        var hit = payload.What;
        if (!Registry.TryGetByHull(hit.Hull, out var ship))
        {
            Drop("unregistered_hull", Guid.Empty, session.OwnControllerId);
            return;
        }

        var damage = new NetworkApplyShipDamage(ship.ShipId, session.OwnControllerId, hit.Kind, hit.Damage, hit.InflictedDamage,
            hit.LocalPoint, AgentIdOf(hit.Attacker), ShipIdOf(hit.Hitter), hit.IsRamDamage, hit.SailIndex);

        // Decided once behind the queued move, so the hit reaches the successor; never deferred a second time.
        if (ship.FollowsBattleHost && IsHostChangeQueued)
        {
            deferred++;
            arrivals.Enqueue(() => SendDeferred(damage), nameof(Handle_BattleShipHit));
            return;
        }

        SendToAuthority(ship, damage);
    }

    // [Mission tick] Sends a hit held behind a queued host change to whoever holds the hull now.
    private void SendDeferred(NetworkApplyShipDamage damage)
    {
        if (disposed) return;

        if (!Registry.TryGet(damage.ShipId, out var ship))
        {
            Drop("unknown_ship", damage.ShipId, damage.SenderControllerId);
            return;
        }

        SendToAuthority(ship, damage);
    }

    // [Game thread] Applies the hit here when this client already holds the hull (mid-swap), otherwise sends it to its authority.
    private void SendToAuthority(NetworkShipInfo ship, NetworkApplyShipDamage damage)
    {
        if (ship.CurrentAuthority == session.OwnControllerId)
        {
            Apply(ship, damage);
            return;
        }

        network.Send(ship.CurrentAuthority, damage);
        sent++;
    }

    // Queues a received hit behind the host change and hull records that arrived before it.
    private void Handle_NetworkApplyShipDamage(MessagePayload<NetworkApplyShipDamage> payload)
    {
        var damage = payload.What;
        GameThread.RunSafe(() =>
        {
            if (Mission.Current == null) return;
            arrivals.Enqueue(() => ApplyNetworkDamage(damage), nameof(Handle_NetworkApplyShipDamage));
        }, context: nameof(Handle_NetworkApplyShipDamage));
    }

    /// <summary>[Game thread] Applies a routed hit when this client holds the hull's authority, otherwise drops it.</summary>
    internal void ApplyNetworkDamage(NetworkApplyShipDamage damage)
    {
        if (disposed || damage == null) return;

        if (!Registry.TryGet(damage.ShipId, out var ship))
        {
            Drop("unknown_ship", damage.ShipId, damage.SenderControllerId);
            return;
        }

        var rejection = ValidateDamage(ship, session.OwnControllerId, damage);
        if (rejection != null)
        {
            Drop(rejection, damage.ShipId, damage.SenderControllerId);
            return;
        }

        Apply(ship, damage);
    }

    /// <summary>Why a routed hit must be dropped, or null to apply it; a hull handed on since the hit was sent is not_authority.</summary>
    internal static string ValidateDamage(NetworkShipInfo ship, string ownControllerId, NetworkApplyShipDamage damage)
    {
        if (ship.CurrentAuthority != ownControllerId) return "not_authority";
        if (!damage.IsValid) return "invalid_damage";
        return null;
    }

    // Replays the hit through the vanilla damage method, with its attacker and hitter resolved here.
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

    // Counts a dropped hit by reason, so inspect tells a migration window from a removed hull.
    private void Drop(string reason, Guid shipId, string senderControllerId)
    {
        drops.TryGetValue(reason, out long count);
        drops[reason] = count + 1;
        Logger.Debug("[NavalDamage] Dropped a hit on hull {ShipId} from {Sender}: {Reason}", shipId, senderControllerId, reason);
    }

    // The attacking agent's registry id, or empty.
    private Guid AgentIdOf(Agent agent) =>
        agent != null && missionComponent.AgentRegistry.TryGetAgentInfo(agent, out var info) ? info.AgentId : Guid.Empty;

    // The hitting hull's ship id, or empty.
    private Guid ShipIdOf(MissionObject hull) =>
        hull != null && Registry.TryGetByHull(hull, out var ship) ? ship.ShipId : Guid.Empty;

    public object Inspect() => new
    {
        sent,
        deferred,
        applied,
        drops = new Dictionary<string, long>(drops),
    };
}
