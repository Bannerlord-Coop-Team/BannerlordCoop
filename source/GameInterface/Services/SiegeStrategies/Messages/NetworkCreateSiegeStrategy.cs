using Common.Messaging;
using ProtoBuf;

namespace GameInterface.Services.SiegeStrategies.Messages;

[ProtoContract(SkipConstructor = true)]
internal class NetworkCreateSiegeStrategy : ICommand
{
    [ProtoMember(1)]
    public string Id { get; }

    public NetworkCreateSiegeStrategy(string id)
    {
        Id = id;
    }
}
