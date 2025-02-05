using Common.Messaging;
using ProtoBuf;

namespace GameInterface.Services.MobileParties.Messages;

[ProtoContract(SkipConstructor = true)]
public class NetworkChangePartyComponent : ICommand
{
    [ProtoMember(1)]
    public string PartyId { get; }

    [ProtoMember(2)]
    public string PartyComponentId { get; }

    public NetworkChangePartyComponent(string partyId, string partyComponentId)
    {
        PartyId = partyId;
        PartyComponentId = partyComponentId;
    }
}