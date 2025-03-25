using Common.Messaging;
using ProtoBuf;

namespace GameInterface.Registry.Auto;
[ProtoContract(SkipConstructor = true)]
class NetworkCreateInstance<T> : ICommand
{
    [ProtoMember(1)]
    public string InstanceId { get; }

    public NetworkCreateInstance(string instanceId)
    {
        InstanceId = instanceId;
    }
}