using Common.Messaging;
using ProtoBuf;

namespace GameInterface.Registry.Auto;

[ProtoContract(SkipConstructor = true)]
readonly struct NetworkDestroyInstance<T> : ICommand
{
    [ProtoMember(1)]
    public readonly string InstanceId;

    public NetworkDestroyInstance(string instanceId)
    {
        InstanceId = instanceId;
    }
}