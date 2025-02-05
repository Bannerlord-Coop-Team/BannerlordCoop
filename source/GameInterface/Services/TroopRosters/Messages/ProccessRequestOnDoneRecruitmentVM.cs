using Common.Messaging;
using LiteNetLib;

namespace GameInterface.Services.TroopRosters.Messages;
public record ProccessRequestOnDoneRecruitmentVM : IEvent
{
    public string MobilePartyId { get; }

    public (string, string, int)[] TroopsInCart { get; }

    public NetPeer ClientWho { get; }

    public ProccessRequestOnDoneRecruitmentVM(string mobilePartyId, (string, string, int)[] troopsInCart, NetPeer clientWho)
    {
        MobilePartyId = mobilePartyId;
        TroopsInCart = troopsInCart;
        ClientWho = clientWho;
    }
}
