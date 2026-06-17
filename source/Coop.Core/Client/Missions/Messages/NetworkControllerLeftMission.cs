using Common.Messaging;
using ProtoBuf;

namespace Coop.Core.Client.Missions.Messages;

[ProtoContract]
internal readonly struct NetworkControllerLeftMission : IEvent
{
    [ProtoMember(1)]
    public readonly string ControllerId;

    public NetworkControllerLeftMission(string controllerId)
    {
        ControllerId = controllerId;
    }
}
