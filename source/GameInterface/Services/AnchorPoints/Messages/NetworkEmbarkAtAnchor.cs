using Common.Messaging;
using ProtoBuf;

namespace GameInterface.Services.AnchorPoints.Messages;

[ProtoContract(SkipConstructor = true)]
internal readonly struct NetworkEmbarkAtAnchor : ICommand
{
    [ProtoMember(1)]
    public readonly string MobilePartyId;

    public NetworkEmbarkAtAnchor(string mobilePartyId)
    {
        MobilePartyId = mobilePartyId;
    }
}
