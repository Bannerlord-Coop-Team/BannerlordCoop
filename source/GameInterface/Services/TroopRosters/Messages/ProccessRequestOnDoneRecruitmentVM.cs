using Common.Messaging;
using LiteNetLib;

namespace GameInterface.Services.TroopRosters.Messages;
public record ProccessRequestOnDoneRecruitmentVM : IEvent
{
    public string MobilePartyId { get; }

    public (string, string, int)[] TroopsInCart { get; }

    public NetPeer ClientWho { get; }

    public int TotalCost { get; }

    public ProccessRequestOnDoneRecruitmentVM(string mobilePartyId, (string, string, int)[] troopsInCart, NetPeer clientWho, int totalCost)
    {
        MobilePartyId = mobilePartyId;
        TroopsInCart = troopsInCart;
        ClientWho = clientWho;
        TotalCost = totalCost;
    }
}
