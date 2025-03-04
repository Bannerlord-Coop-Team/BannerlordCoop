using Common.Messaging;
using ProtoBuf;

namespace Coop.Core.Client.Services.TroopRosters.Messages;

[ProtoContract(SkipConstructor = true)]
public record ClientRequestOnDoneRecruitmentVM : IEvent
{
    [ProtoMember(1)]
    public string MobilePartyId { get; }
    [ProtoMember(2)]
    public (string, string, int)[] TroopsInCart { get; }
    [ProtoMember(3)]
    public int TotalCost { get; }

    public ClientRequestOnDoneRecruitmentVM(string mobilePartyId, (string, string, int)[] troopsInCart, int totalCost)
    {
        MobilePartyId = mobilePartyId;
        TroopsInCart = troopsInCart;
        TotalCost = totalCost;
    }
}
