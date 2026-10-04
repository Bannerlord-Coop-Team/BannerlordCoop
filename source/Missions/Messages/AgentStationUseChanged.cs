using Common.Messaging;
using TaleWorlds.MountAndBlade;

namespace Missions.Messages;

/// <summary>[Game thread] An agent started or stopped using a usable point in a naval mission.</summary>
public readonly struct AgentStationUseChanged : IEvent
{
    public readonly Agent Agent;
    public readonly UsableMissionObject Point;
    public readonly bool InUse;

    public AgentStationUseChanged(Agent agent, UsableMissionObject point, bool inUse)
    {
        Agent = agent;
        Point = point;
        InUse = inUse;
    }
}
