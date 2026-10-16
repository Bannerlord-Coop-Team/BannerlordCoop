using Missions.Battles;
using Missions.Messages;
using System;
using System.Collections.Generic;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace Missions.Agents.Handlers;

/// <summary>
/// [Game thread] The <see cref="IShipDeckSeam"/> of a naval battle: hull-local poses over the registered hulls, and the
/// seats this client's station uses hold. Configured on the movement and action handlers for the mission's lifetime.
/// </summary>
public interface INavalDeckPoses : IShipDeckSeam, IDisposable
{
    /// <summary>[Mission tick] Advances the clock deck speeds are measured on.</summary>
    void Tick(float dt);

    /// <summary>The registered hull an on-foot agent stands on and its hull-local position.</summary>
    bool TryGetDeckPose(Agent agent, Vec3 worldPosition, out Guid deckShip, out Vec3 deckLocal);
}

/// <inheritdoc cref="INavalDeckPoses"/>
public class NavalDeckPoses : INavalDeckPoses
{
    private readonly ICoopMissionComponent missionComponent;
    private readonly INavalShipEngine engine;
    private readonly IAgentStationUseReplicator stations;

    private readonly Dictionary<Guid, DeckSpeedSample> deckSpeeds = new Dictionary<Guid, DeckSpeedSample>();
    private float elapsed;

    public NavalDeckPoses(ICoopMissionComponent missionComponent, INavalShipEngine engine, IAgentStationUseReplicator stations)
    {
        this.missionComponent = missionComponent;
        this.engine = engine;
        this.stations = stations;

        missionComponent.AgentMovementHandler.ConfigureShipDeck(this);
        missionComponent.AgentActionHandler.ConfigureShipDeck(this);
    }

    public void Dispose()
    {
        missionComponent.AgentMovementHandler.ConfigureShipDeck(null);
        missionComponent.AgentActionHandler.ConfigureShipDeck(null);
    }

    private INetworkShipRegistry Registry => missionComponent.ShipRegistry;

    public void Tick(float dt) => elapsed += Math.Max(0f, dt);

    // Any owned on-foot agent on a registered hull sends a hull-local pose, so it rides the deck on every peer.
    public bool TryCaptureDeck(CoopAgentInfo info, Vec3 worldPosition, out Guid deckShip, out Vec3 deckLocal, out float deckSpeed)
    {
        deckSpeed = 0f;
        if (!TryGetDeckPose(info?.Agent, worldPosition, out deckShip, out deckLocal)) return false;

        deckSpeed = DeckSpeed(info.AgentId, deckShip, deckLocal);
        return true;
    }

    public bool TryGetDeckPose(Agent agent, Vec3 worldPosition, out Guid deckShip, out Vec3 deckLocal)
    {
        deckShip = Guid.Empty;
        deckLocal = Vec3.Zero;
        var hull = engine.GetSupportHull(agent);
        if (hull == null || !Registry.TryGetByHull(hull, out var ship)) return false;

        var frame = engine.GetFrame(hull);
        if (!FrameValidation.IsValid(frame)) return false;

        deckLocal = frame.TransformToLocalNonOrthogonal(worldPosition);
        if (!deckLocal.IsValid) return false;

        deckShip = ship.ShipId;
        return true;
    }

    // Finite difference of hull-local positions, so the puppet's walk throttle never includes hull motion.
    private float DeckSpeed(Guid agentId, Guid shipId, Vec3 local)
    {
        deckSpeeds.TryGetValue(agentId, out var previous);
        float dt = elapsed - previous.Time;
        if (previous.Ship != shipId || dt < 0f || dt > 0.5f)
        {
            deckSpeeds[agentId] = new DeckSpeedSample(shipId, local, elapsed, 0f);
            return 0f;
        }

        if (dt < 0.005f) return previous.Speed;

        float speed = (local - previous.Local).AsVec2.Length / dt;
        deckSpeeds[agentId] = new DeckSpeedSample(shipId, local, elapsed, speed);
        return speed;
    }

    public bool TryResolveDeckFrame(Agent agent, Guid deckShip, out MatrixFrame hullFrame)
    {
        hullFrame = default;
        if (!Registry.TryGet(deckShip, out var ship)) return false;

        hullFrame = engine.GetFrame(ship.Hull);
        return FrameValidation.IsValid(hullFrame);
    }

    public bool IsSeatedAtStation(CoopAgentInfo info) => info != null && stations.IsOwnSeat(info.AgentId);

    private readonly struct DeckSpeedSample
    {
        public DeckSpeedSample(Guid ship, Vec3 local, float time, float speed)
        {
            Ship = ship;
            Local = local;
            Time = time;
            Speed = speed;
        }

        public Guid Ship { get; }
        public Vec3 Local { get; }
        public float Time { get; }
        public float Speed { get; }
    }
}
