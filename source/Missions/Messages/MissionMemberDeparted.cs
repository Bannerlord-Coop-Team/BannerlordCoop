using Common.Messaging;

namespace Missions.Messages;

/// <summary>
/// [Server, local] A controller left or dropped from a mission instance, published by the membership handler once it has
/// resolved the ids and the remaining membership. Host election promotes a successor on it or drops the successor.
/// </summary>
public readonly struct MissionMemberDeparted : IEvent
{
    public readonly string ControllerId;
    public readonly string InstanceId;
    /// <summary>A graceful leave, so the player's final health snapshot came before it.</summary>
    public readonly bool WasRetreat;
    public readonly bool IsInstanceEmpty;
    /// <summary>The departure withdraws the player's fielded party: a leave, or a disconnect at sea (D4).</summary>
    public readonly bool Withdraws;

    public MissionMemberDeparted(string controllerId, string instanceId, bool wasRetreat, bool isInstanceEmpty, bool withdraws)
    {
        ControllerId = controllerId;
        InstanceId = instanceId;
        WasRetreat = wasRetreat;
        IsInstanceEmpty = isInstanceEmpty;
        Withdraws = withdraws;
    }
}
