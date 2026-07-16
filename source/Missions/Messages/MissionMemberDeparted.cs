using Common.Messaging;

namespace Missions.Messages;

/// <summary>
/// [Server, local] A controller left or dropped from a mission instance, published once the membership
/// handler has resolved the controller id, instance id, and remaining membership (so it is not a networked
/// message). Battle host election uses it to promote a successor when the departed controller was the host, or
/// to drop the controller from the successor line otherwise.
/// <para>
/// <see cref="WasRetreat"/> means the player's party withdraws and the battle reserve must forget it for a
/// clean re-spawn on rejoin. Both a graceful retreat and a campaign-server disconnect use that behavior.
/// </para>
/// </summary>
public readonly struct MissionMemberDeparted : IEvent
{
    public readonly string ControllerId;
    public readonly string InstanceId;
    public readonly bool WasRetreat;
    public readonly bool IsInstanceEmpty;

    public MissionMemberDeparted(string controllerId, string instanceId, bool wasRetreat, bool isInstanceEmpty)
    {
        ControllerId = controllerId;
        InstanceId = instanceId;
        WasRetreat = wasRetreat;
        IsInstanceEmpty = isInstanceEmpty;
    }
}
