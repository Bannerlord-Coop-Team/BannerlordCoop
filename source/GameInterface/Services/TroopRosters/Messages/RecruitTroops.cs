using Common.Messaging;
using LiteNetLib;

namespace GameInterface.Services.TroopRosters.Messages;

public readonly struct RecruitTroops : ICommand
{
    public readonly string MobilePartyId;

    public readonly TroopInfo[] TroopsInCart;

    public readonly object Who;

    public RecruitTroops(string mobilePartyId, TroopInfo[] troopsInCart, object who)
    {
        MobilePartyId = mobilePartyId;
        TroopsInCart = troopsInCart;
        Who = who;
    }
}
