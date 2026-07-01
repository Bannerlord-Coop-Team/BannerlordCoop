using Common.Messaging;

namespace Missions.Messages;

/// <summary>
/// [Server, local] A controller left or dropped from a mission instance, published once the membership
/// handler has resolved the controller id and instance id (so it is not a networked message). Battle host
/// election uses it to promote a successor when the departed controller was the host, or to drop the
/// controller from the successor line otherwise.
/// <para>
/// <see cref="WasRetreat"/> distinguishes a graceful leave (retreat — the player's troops despawned, so the
/// battle reserve must forget its party for a clean re-spawn on rejoin) from an ungraceful drop (disconnect —
/// the host adopts the troops, so the reserve pointer is kept).
/// </para>
/// </summary>
public readonly struct MissionMemberDeparted : IEvent
{
    public readonly string ControllerId;
    public readonly string InstanceId;
    public readonly bool WasRetreat;

    public MissionMemberDeparted(string controllerId, string instanceId, bool wasRetreat)
    {
        ControllerId = controllerId;
        InstanceId = instanceId;
        WasRetreat = wasRetreat;
    }
}
