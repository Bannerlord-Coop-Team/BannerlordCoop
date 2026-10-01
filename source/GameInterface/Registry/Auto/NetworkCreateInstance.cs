using Common.Messaging;
using ProtoBuf;

namespace GameInterface.Registry.Auto;
[ProtoContract(SkipConstructor = true)]
readonly struct NetworkCreateInstance<T> : ICommand
{
    [ProtoMember(1)]
    public readonly string InstanceId;

    [ProtoMember(2)]
    public readonly uint InstanceHandle;

    public NetworkCreateInstance(string instanceId, uint instanceHandle)
    {
        InstanceId = instanceId;
        InstanceHandle = instanceHandle;
    }
}
