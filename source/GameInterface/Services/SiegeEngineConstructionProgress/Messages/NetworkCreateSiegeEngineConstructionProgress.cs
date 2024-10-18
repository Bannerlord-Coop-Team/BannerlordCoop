using Common.Messaging;
using ProtoBuf;

namespace GameInterface.Services.SiegeEngineConstructionProgresss.Messages;

[ProtoContract(SkipConstructor = true)]
internal class NetworkCreateSiegeEngineConstructionProgress : ICommand
{
    [ProtoMember(1)]
    public string Id { get; }

    public NetworkCreateSiegeEngineConstructionProgress(string id)
    {
        Id = id;
    }
}