using Common.Messaging;
using TaleWorlds.MountAndBlade;

namespace Missions.Messages;

/// <summary>
/// [Game thread] Vanilla's capture timer ran out for <see cref="CaptorAgent"/> at <see cref="Hull"/>'s helm in a co-op battle;
/// vanilla's swap was skipped, so the hull's authority decides it instead.
/// </summary>
public readonly struct ShipCaptureTimerElapsed : IEvent
{
    public readonly MissionObject Hull;
    public readonly Agent CaptorAgent;

    public ShipCaptureTimerElapsed(MissionObject hull, Agent captorAgent)
    {
        Hull = hull;
        CaptorAgent = captorAgent;
    }
}
