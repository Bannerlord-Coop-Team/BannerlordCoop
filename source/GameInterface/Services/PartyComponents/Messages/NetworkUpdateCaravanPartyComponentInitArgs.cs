using Common.Messaging;
using ProtoBuf;

namespace GameInterface.Services.PartyComponents.Messages;

[ProtoContract]
internal readonly struct NetworkUpdateCaravanPartyComponentInitArgs : IEvent
{
    [ProtoMember(1)]
    public readonly string CaravanPartyComponentId;
    [ProtoMember(2)]
    public readonly string CaravanLeaderId;
    [ProtoMember(3)]
    public readonly string CaravanItemRosterId;
    [ProtoMember(4)]
    public readonly string PartyTemplateObjectId;

    public NetworkUpdateCaravanPartyComponentInitArgs(
        string caravanPartyComponentId,
        string caravanLeaderId,
        string caravanItemRosterId,
        string partyTemplateObjectId)
    {
        CaravanPartyComponentId = caravanPartyComponentId;
        CaravanLeaderId = caravanLeaderId;
        CaravanItemRosterId = caravanItemRosterId;
        PartyTemplateObjectId = partyTemplateObjectId;
    }
}
