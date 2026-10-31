using Missions;
using Missions.Agents.Handlers;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace E2E.Tests.Environment.Mock;

/// <summary>An <see cref="IShipDeckSeam"/> driven by test delegates; an unset delegate captures or resolves nothing.</summary>
public sealed class FakeShipDeckSeam : IShipDeckSeam
{
    /// <summary>The hull, hull-local position and deck speed of an owned agent, or null when it is on no hull.</summary>
    public Func<CoopAgentInfo, Vec3, (Guid Ship, Vec3 Local, float Speed)?>? Capture { get; set; }

    /// <summary>The local hull frame a received deck pose is relative to, or null when it does not resolve.</summary>
    public Func<Agent, Guid, MatrixFrame?>? Resolve { get; set; }

    public bool TryCaptureDeck(CoopAgentInfo info, Vec3 worldPosition, float time, out Guid deckShip, out Vec3 deckLocal, out float deckSpeed)
    {
        var pose = Capture?.Invoke(info, worldPosition);
        deckShip = pose?.Ship ?? Guid.Empty;
        deckLocal = pose?.Local ?? Vec3.Zero;
        deckSpeed = pose?.Speed ?? 0f;
        return pose != null;
    }

    public bool TryResolveDeckFrame(Agent agent, Guid deckShip, out MatrixFrame hullFrame)
    {
        var frame = Resolve?.Invoke(agent, deckShip);
        hullFrame = frame ?? default;
        return frame != null;
    }
}
