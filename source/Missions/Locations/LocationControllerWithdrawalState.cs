using System;
using System.Collections.Generic;

namespace Missions.Locations;

/// <summary>Tracks withdrawn controllers and former NPC hosts for one location mission.</summary>
public interface ILocationControllerWithdrawalState
{
    void MarkEntered(string controllerId);
    void MarkWithdrawn(string controllerId, bool wasHost);
    bool IsWithdrawn(string controllerId, out bool wasFormerHost);
    void RetainFormerHostRecord(Guid agentId);
    bool IsRetainedFormerHostRecord(string controllerId, Guid agentId);
}

/// <inheritdoc cref="ILocationControllerWithdrawalState"/>
public class LocationControllerWithdrawalState : ILocationControllerWithdrawalState
{
    private readonly object gate = new object();
    private readonly HashSet<string> withdrawnControllers = new HashSet<string>();
    private readonly HashSet<string> formerHostControllers = new HashSet<string>();
    private readonly HashSet<Guid> retainedFormerHostAgentIds = new HashSet<Guid>();

    public void MarkEntered(string controllerId)
    {
        lock (gate)
        {
            withdrawnControllers.Remove(controllerId);
        }
    }

    public void MarkWithdrawn(string controllerId, bool wasHost)
    {
        lock (gate)
        {
            withdrawnControllers.Add(controllerId);
            if (wasHost) formerHostControllers.Add(controllerId);
        }
    }

    public bool IsWithdrawn(string controllerId, out bool wasFormerHost)
    {
        lock (gate)
        {
            bool isWithdrawn = withdrawnControllers.Contains(controllerId);
            wasFormerHost = isWithdrawn && formerHostControllers.Contains(controllerId);
            return isWithdrawn;
        }
    }

    public void RetainFormerHostRecord(Guid agentId)
    {
        if (agentId == Guid.Empty) return;

        lock (gate)
        {
            retainedFormerHostAgentIds.Add(agentId);
        }
    }

    public bool IsRetainedFormerHostRecord(string controllerId, Guid agentId)
    {
        if (agentId == Guid.Empty) return false;

        lock (gate)
        {
            if (retainedFormerHostAgentIds.Contains(agentId)) return true;
            if (!withdrawnControllers.Contains(controllerId)
                || !formerHostControllers.Contains(controllerId))
            {
                return false;
            }

            retainedFormerHostAgentIds.Add(agentId);
            return true;
        }
    }
}
