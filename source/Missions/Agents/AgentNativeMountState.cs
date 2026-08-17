using System;
using TaleWorlds.MountAndBlade;

namespace Missions.Agents;

public interface IAgentNativeMountState
{
    bool HasMountedPair(Agent rider);
}

public class AgentNativeMountState : IAgentNativeMountState
{
    public bool HasMountedPair(Agent rider)
    {
        if (rider == null || !rider.IsActive()) return false;

        Agent mount = MBAPI.IMBAgent.GetMountAgent(rider.GetPtr());
        if (mount == null || !mount.IsActive()) return false;

        return ReferenceEquals(
            MBAPI.IMBAgent.GetRiderAgent(mount.GetPtr()),
            rider);
    }
}
