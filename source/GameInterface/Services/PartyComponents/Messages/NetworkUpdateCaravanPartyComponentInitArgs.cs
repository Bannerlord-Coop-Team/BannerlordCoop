using Common.Messaging;
using ProtoBuf;
using TaleWorlds.Core;

namespace GameInterface.Services.PartyComponents.Messages;

[ProtoContract]
internal readonly struct NetworkUpdateCaravanPartyComponentInitArgs : IEvent
{
    [ProtoMember(1)]
    public readonly string CaravanPartyComponentId;
    [ProtoMember(2)]
    public readonly string CaravanLeaderId;
    [ProtoMember(3)]
    public readonly ItemRosterElement[] CaravanItems;
    [ProtoMember(4)]
    public readonly string PartyTemplateObjectId;

    public NetworkUpdateCaravanPartyComponentInitArgs(
        string caravanPartyComponentId,
        string caravanLeaderId,
        ItemRosterElement[] caravanItems,
        string partyTemplateObjectId)
    {
        CaravanPartyComponentId = caravanPartyComponentId;
        CaravanLeaderId = caravanLeaderId;
        CaravanItems = caravanItems;
        PartyTemplateObjectId = partyTemplateObjectId;
    }
}
