using Common.Messaging;
using ProtoBuf;

namespace GameInterface.Services.MapEvents.Messages.Start;

[ProtoContract]
internal readonly struct NetworkPlayerPartyInteracted : IEvent
{
    [ProtoMember(1)]
    public readonly string RequestingPartyId;
    [ProtoMember(2)]
    public readonly string TargetPartyId;

    public NetworkPlayerPartyInteracted(string requestingPartyId, string targetPartyId)
    {
        RequestingPartyId = requestingPartyId;
        TargetPartyId = targetPartyId;
    }
}
