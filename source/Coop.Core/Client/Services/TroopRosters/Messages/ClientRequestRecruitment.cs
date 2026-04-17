using Common.Messaging;
using GameInterface.Services.TroopRosters.Messages;
using ProtoBuf;

namespace Coop.Core.Client.Services.TroopRosters.Messages;

[ProtoContract(SkipConstructor = true)]
public readonly struct ClientRequestRecruitment : IEvent
{
    [ProtoMember(1)]
    public string MobilePartyId { get; }
    [ProtoMember(2)]
    public TroopInfo[] TroopsInCart { get; }

    public ClientRequestRecruitment(string mobilePartyId, TroopInfo[] troopsInCart)
    {
        MobilePartyId = mobilePartyId;
        TroopsInCart = troopsInCart;
    }
}
