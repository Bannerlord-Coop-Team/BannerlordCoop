using Missions.Battles;
using Missions.Messages;
using System;
using System.Collections.Generic;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace Missions.Agents.Handlers;

/// <summary>A naval battle's view of agents on hulls, which the movement handler asks while it is configured: owned agents
/// on a hull send hull-local poses, and received ones rebase on the local hull.</summary>
public interface IShipDeckSeam
{
    /// <summary>[Game thread] An owned on-foot agent's hull-local pose and its deck-relative speed measured on the sender's
    /// <paramref name="time"/> clock; false when it is on no registered hull.</summary>
    bool TryCaptureDeck(CoopAgentInfo info, Vec3 worldPosition, float time, out Guid deckShip, out Vec3 deckLocal, out float deckSpeed);

    /// <summary>[Game thread] The local hull, owned or copied, that a received deck pose is relative to.</summary>
    bool TryResolveDeckFrame(Agent agent, Guid deckShip, out MatrixFrame hullFrame);
}

/// <summary>
/// [Game thread] The <see cref="IShipDeckSeam"/> of a naval battle: hull-local poses over the registered hulls, configured on
/// the movement handler for the mission's lifetime.
/// </summary>
public interface INavalDeckPoses : IShipDeckSeam, IDisposable
{
    /// <summary>The registered hull an on-foot agent stands on and its hull-local position.</summary>
    bool TryGetDeckPose(Agent agent, Vec3 worldPosition, out Guid deckShip, out Vec3 deckLocal);
}

/// <inheritdoc cref="INavalDeckPoses"/>
public class NavalDeckPoses : INavalDeckPoses
{
    // A connected plank reaches past the side of its source hull; vanilla connects a bridge within 10 m
    // (ShipAttachmentMachine.BridgeConnectedInteractionDistanceSquared = 100).
    internal const float FootprintMarginMetres = 15f;

    // A still agent's hull-local position jitters by float32 steps (2^-12 m at 3600 m from the origin, up to ~3 mm per
    // sample), so one 33 ms sample reads up to 0.1 m/s; a 0.1 s baseline cuts that to about 0.03 m/s.
    internal const float DeckSpeedBaselineSeconds = 0.1f;

    // Deck speeds at or under this are idle and report 0; a walking crew member moves at 1 m/s or more.
    // The deck movement probe classes a send as idle by the same speed.
    internal const float IdleDeckSpeed = 0.1f;

    // A baseline older than this, e.g. after the agent sent no deck pose for a while, restarts the measurement.
    private const float DeckSpeedResetSeconds = 0.5f;

    private readonly ICoopMissionComponent missionComponent;
    private readonly INavalShipEngine engine;

    private readonly Dictionary<Guid, DeckSpeedSample> deckSpeeds = new Dictionary<Guid, DeckSpeedSample>();

    // Configures this seam on the movement handler for the mission's lifetime.
    public NavalDeckPoses(ICoopMissionComponent missionComponent, INavalShipEngine engine)
    {
        this.missionComponent = missionComponent;
        this.engine = engine;

        missionComponent.AgentMovementHandler.ConfigureShipDeck(this);
    }

    // Takes the seam off the movement handler.
    public void Dispose() => missionComponent.AgentMovementHandler.ConfigureShipDeck(null);

    // The mission's hull registry, which maps hulls to their network ids.
    private INetworkShipRegistry Registry => missionComponent.ShipRegistry;

    // Any owned on-foot agent on a registered hull sends a hull-local pose, so it rides the deck on every peer.
    public bool TryCaptureDeck(CoopAgentInfo info, Vec3 worldPosition, float time, out Guid deckShip, out Vec3 deckLocal, out float deckSpeed)
    {
        deckSpeed = 0f;
        if (!TryGetDeckPose(info?.Agent, worldPosition, out deckShip, out deckLocal)) return false;

        deckSpeed = DeckSpeed(info.AgentId, deckShip, deckLocal, time);
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

    // Deck speed over a baseline of at least DeckSpeedBaselineSeconds, held in between, with idle noise snapped to 0, so the
    // puppet's walk throttle excludes hull motion and a still crew member's speed never changes.
    private float DeckSpeed(Guid agentId, Guid shipId, Vec3 local, float time)
    {
        deckSpeeds.TryGetValue(agentId, out var baseline);
        float dt = time - baseline.Time;
        if (baseline.Ship != shipId || dt < 0f || dt > DeckSpeedResetSeconds)
        {
            deckSpeeds[agentId] = new DeckSpeedSample(shipId, local, time, 0f);
            return 0f;
        }

        if (dt < DeckSpeedBaselineSeconds) return baseline.Speed;

        float speed = (local - baseline.Local).AsVec2.Length / dt;
        if (speed <= IdleDeckSpeed) speed = 0f;
        deckSpeeds[agentId] = new DeckSpeedSample(shipId, local, time, speed);
        return speed;
    }

    public bool TryResolveDeckFrame(Agent agent, Guid deckShip, out MatrixFrame hullFrame)
    {
        hullFrame = default;
        if (!Registry.TryGet(deckShip, out var ship)) return false;

        hullFrame = engine.GetFrame(ship.Hull);
        return FrameValidation.IsValid(hullFrame);
    }

    // An agent's deck-speed baseline: the ship, its hull-local position, the time and the speed measured then.
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
