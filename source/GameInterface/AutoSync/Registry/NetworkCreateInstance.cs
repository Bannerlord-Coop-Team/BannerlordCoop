using Common.Messaging;
using ProtoBuf;

namespace GameInterface.AutoSync.Registry;
[ProtoContract(SkipConstructor = true)]
class NetworkCreateInstance<T> : ICommand where T : class
{
    [ProtoMember(1)]
    public string InstanceId { get; }

    public NetworkCreateInstance(string instanceId)
    {
        InstanceId = instanceId;
    }
}