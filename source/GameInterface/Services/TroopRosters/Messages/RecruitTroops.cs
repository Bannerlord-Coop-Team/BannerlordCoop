using Common.Messaging;
using LiteNetLib;

namespace GameInterface.Services.TroopRosters.Messages;

public readonly struct RecruitTroops : ICommand
{
    public readonly string MobilePartyId;

    public readonly TroopInfo[] TroopsInCart;

    public readonly NetPeer Who;

    public RecruitTroops(string mobilePartyId, TroopInfo[] troopsInCart, NetPeer who)
    {
        MobilePartyId = mobilePartyId;
        TroopsInCart = troopsInCart;
        Who = who;
    }
}
