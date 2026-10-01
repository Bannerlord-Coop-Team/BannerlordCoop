using Common.Messaging;
using ProtoBuf;

namespace GameInterface.Registry.Auto;

[ProtoContract(SkipConstructor = true)]
readonly struct NetworkDestroyInstance<T> : ICommand
{
    [ProtoMember(1)]
    public readonly uint InstanceHandle;

    public NetworkDestroyInstance(uint instanceHandle)
    {
        InstanceHandle = instanceHandle;
    }
}
