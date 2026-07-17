using LiteNetLib;
using System.Collections.Generic;

namespace GameInterface.Services.Missions;

/// <summary>
/// Session-scoped membership for clients that have entered a mission and have not left it yet.
/// </summary>
public interface IMissionMembershipRegistry
{
    void Enter(string instanceId, string controllerId, NetPeer peer);
    void Leave(string instanceId, string controllerId, NetPeer peer);
    bool IsControllerInMission(string controllerId);
}

/// <inheritdoc cref="IMissionMembershipRegistry"/>
public sealed class MissionMembershipRegistry : IMissionMembershipRegistry
{
    private readonly Dictionary<string, (string instanceId, NetPeer peer)> membershipsByController = new();
    private readonly object gate = new();

    public void Enter(string instanceId, string controllerId, NetPeer peer)
    {
        if (string.IsNullOrEmpty(instanceId) || string.IsNullOrEmpty(controllerId) || peer == null)
            return;

        lock (gate)
        {
            membershipsByController[controllerId] = (instanceId, peer);
        }
    }

    public void Leave(string instanceId, string controllerId, NetPeer peer)
    {
        if (string.IsNullOrEmpty(instanceId) || string.IsNullOrEmpty(controllerId) || peer == null)
            return;

        lock (gate)
        {
            if (membershipsByController.TryGetValue(controllerId, out var current) &&
                current.instanceId == instanceId &&
                ReferenceEquals(current.peer, peer))
            {
                membershipsByController.Remove(controllerId);
            }
        }
    }

    public bool IsControllerInMission(string controllerId)
    {
        if (string.IsNullOrEmpty(controllerId))
            return false;

        lock (gate)
        {
            return membershipsByController.ContainsKey(controllerId);
        }
    }
}
