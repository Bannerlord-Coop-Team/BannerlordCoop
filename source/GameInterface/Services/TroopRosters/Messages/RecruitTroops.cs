using Common.Messaging;
using LiteNetLib;

namespace GameInterface.Services.TroopRosters.Messages;

public readonly struct RecruitTroops : ICommand
{
    public readonly string MobilePartyId;

    public readonly TroopInfo[] TroopsInCart;

    public RecruitTroops(string mobilePartyId, TroopInfo[] troopsInCart)
    {
        MobilePartyId = mobilePartyId;
        TroopsInCart = troopsInCart;
    }
}
