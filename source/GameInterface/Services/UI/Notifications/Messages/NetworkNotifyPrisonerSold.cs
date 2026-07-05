using Common.Messaging;
using GameInterface.Services.TroopRosters.Data;
using ProtoBuf;

namespace GameInterface.Services.UI.Notifications.Messages;

[ProtoContract(SkipConstructor = true)]
public readonly struct NetworkNotifyPrisonerSold : ICommand
{
    [ProtoMember(1)]
    public readonly string SellerPartyId;

    [ProtoMember(2)]
    public readonly string BuyerPartyId;

    [ProtoMember(3)]
    public readonly TroopRosterData Prisoners;

    public NetworkNotifyPrisonerSold(string sellerPartyId, string buyerPartyId, TroopRosterData prisoners)
    {
        SellerPartyId = sellerPartyId;
        BuyerPartyId = buyerPartyId;
        Prisoners = prisoners;
    }
}
