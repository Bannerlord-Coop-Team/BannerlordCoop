using Missions.Battles;
using Missions.Messages;
using System;
using System.Collections.Generic;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace Missions.Agents.Handlers;

/// <summary>
/// A naval battle's view of agents on hulls, which the movement and action handlers ask while it is configured: owned
/// agents on a hull send hull-local poses, received ones rebase on the local hull, and seated ones leave their pose and
/// actions to the station.
/// </summary>
public interface IShipDeckSeam
{
    /// <summary>[Game thread] An owned on-foot agent's hull-local pose and deck-relative speed; false when it is on no registered hull.</summary>
    bool TryCaptureDeck(CoopAgentInfo info, Vec3 worldPosition, out Guid deckShip, out Vec3 deckLocal, out float deckSpeed);

    /// <summary>[Game thread] The local hull, owned or copied, that a received deck pose is relative to.</summary>
    bool TryResolveDeckFrame(Agent agent, Guid deckShip, out MatrixFrame hullFrame);

    /// <summary>Whether this client seated the agent at a ship station, so it sends no world movement or actions for it.</summary>
    bool IsSeatedAtStation(CoopAgentInfo info);
}

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
    // A connected plank reaches past the side of its source hull; vanilla connects a bridge within 10 m
    // (ShipAttachmentMachine.BridgeConnectedInteractionDistanceSquared = 100).
    internal const float FootprintMarginMetres = 15f;

    private readonly ICoopMissionComponent missionComponent;
    private readonly INavalShipEngine engine;
    private readonly IAgentStationUseReplicator stations;

    private readonly Dictionary<Guid, DeckSpeedSample> deckSpeeds = new Dictionary<Guid, DeckSpeedSample>();
    private float elapsed;

    // Configures this seam on the movement and action handlers for the mission's lifetime.
    public NavalDeckPoses(ICoopMissionComponent missionComponent, INavalShipEngine engine, IAgentStationUseReplicator stations)
    {
        this.missionComponent = missionComponent;
        this.engine = engine;
        this.stations = stations;

        missionComponent.AgentMovementHandler.ConfigureShipDeck(this);
        missionComponent.AgentActionHandler.ConfigureShipDeck(this);
    }

    // Takes the seam off both handlers.
    public void Dispose()
    {
        missionComponent.AgentMovementHandler.ConfigureShipDeck(null);
        missionComponent.AgentActionHandler.ConfigureShipDeck(null);
    }

    // The mission's hull registry, which maps hulls to their network ids.
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
        // Vanilla's on-ship check has no distance, so a latched stepped ship can report an agent hundreds of metres off it.
        if (engine.TryGetFootprint(hull, out var min, out var max) && !IsOverFootprint(deckLocal, min, max))
        {
            deckLocal = Vec3.Zero;
            return false;
        }

        deckShip = ship.ShipId;
        return true;
    }

    /// <summary>Whether a hull-local position is over the hull's footprint widened by <see cref="FootprintMarginMetres"/>;
    /// a pose farther out goes as a world pose, since the copy's small attitude difference times that lever arm misplaces it.</summary>
    internal static bool IsOverFootprint(Vec3 local, Vec2 min, Vec2 max) =>
        local.x >= min.x - FootprintMarginMetres && local.x <= max.x + FootprintMarginMetres
        && local.y >= min.y - FootprintMarginMetres && local.y <= max.y + FootprintMarginMetres;

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

    // An agent's last deck-speed sample: the ship, its hull-local position, the time and the speed measured then.
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
