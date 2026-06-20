using Common.Messaging;
using ProtoBuf;

namespace GameInterface.Missions.Messages;

/// <summary>
/// Broadcast to P2P mesh peers when the local player deliberately leaves the mission/location. Peers
/// despawn and deregister the leaving controller's party — its hero and any troops — outright (see
/// <c>CoopTavernsController.Handle_LeaveMission</c>). <see cref="ControllerId"/> is the leaver's identity,
/// i.e. the <see cref="MissionParty.OriginalOwner"/> of the party/parties to remove.
/// <para>
/// This is the graceful path. An ungraceful exit (crash, network drop) is handled by the disconnect path
/// instead — and in a battle that hands the party to the host rather than removing it.
/// </para>
/// </summary>
[ProtoContract]
public readonly struct NetworkLeaveMission : IEvent
{
    [ProtoMember(1)]
    public readonly string ControllerId;

    public NetworkLeaveMission(string controllerId)
    {
        ControllerId = controllerId;
    }
}
