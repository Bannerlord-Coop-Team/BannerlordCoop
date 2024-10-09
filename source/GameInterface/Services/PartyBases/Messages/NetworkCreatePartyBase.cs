using Common.Messaging;
using ProtoBuf;

namespace GameInterface.Services.PartyBases.Messages;

[ProtoContract(SkipConstructor = true)]
internal class NetworkCreatePartyBase : ICommand
{
    public NetworkCreatePartyBase(string id)
    {
        Id = id;
    }

    [ProtoMember(1)]
    public string Id { get; }
}
