using Common.Messaging;
using ProtoBuf;

namespace GameInterface.Registry.Auto;

[ProtoContract(SkipConstructor = true)]
class NetworkDestroyInstance<T> : ICommand
{
    [ProtoMember(1)]
    public string InstanceId { get; }

    public NetworkDestroyInstance(string instanceId)
    {
        InstanceId = instanceId;
    }
}