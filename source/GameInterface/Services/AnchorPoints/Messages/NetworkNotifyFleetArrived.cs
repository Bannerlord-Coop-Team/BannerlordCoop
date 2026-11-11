using Common.Messaging;
using ProtoBuf;

namespace GameInterface.Services.AnchorPoints.Messages;

[ProtoContract(SkipConstructor = true)]
internal readonly struct NetworkNotifyFleetArrived : ICommand
{
    [ProtoMember(1)]
    public readonly string MobilePartyId;

    public NetworkNotifyFleetArrived(string mobilePartyId)
    {
        MobilePartyId = mobilePartyId;
    }
}
