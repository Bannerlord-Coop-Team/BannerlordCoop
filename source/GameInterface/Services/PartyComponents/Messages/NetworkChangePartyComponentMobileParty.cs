using Common.Messaging;
using ProtoBuf;

namespace GameInterface.Services.PartyComponents.Messages;
[ProtoContract(SkipConstructor = true)]
internal class NetworkChangePartyComponentMobileParty : ICommand
{
    public NetworkChangePartyComponentMobileParty(string componentId, string partyId)
    {
        ComponentId = componentId;
        PartyId = partyId;
    }

    [ProtoMember(1)]
    public string ComponentId { get; }
    [ProtoMember(2)]
    public string PartyId { get; }
}
