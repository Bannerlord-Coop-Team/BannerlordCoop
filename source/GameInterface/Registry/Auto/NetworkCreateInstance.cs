using Common.Messaging;
using ProtoBuf;

namespace GameInterface.Registry.Auto;
[ProtoContract(SkipConstructor = true)]
readonly struct NetworkCreateInstance<T> : ICommand
{
    [ProtoMember(1)]
    public readonly string InstanceId;

    public NetworkCreateInstance(string instanceId)
    {
        InstanceId = instanceId;
    }
}