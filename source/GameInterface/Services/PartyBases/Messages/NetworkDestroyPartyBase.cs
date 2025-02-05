using Common.Messaging;
using ProtoBuf;

namespace GameInterface.Services.PartyBases.Messages;

[ProtoContract(SkipConstructor = true)]
internal class NetworkDestroyPartyBase : ICommand
{
    public NetworkDestroyPartyBase(string id)
    {
        Id = id;
    }

    [ProtoMember(1)]
    public string Id { get; }
}
