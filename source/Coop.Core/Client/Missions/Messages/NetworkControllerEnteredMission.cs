using Common.Messaging;
using ProtoBuf;

namespace Coop.Core.Client.Missions.Messages;

[ProtoContract]
internal readonly struct NetworkControllerEnteredMission : IEvent
{
    [ProtoMember(1)]
    public readonly string ControllerId;

    public NetworkControllerEnteredMission(string controllerId)
    {
        ControllerId = controllerId;
    }
}
