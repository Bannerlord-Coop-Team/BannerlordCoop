using Common.Messaging;

namespace GameInterface.Services.TroopRosters.Messages;
public record OnDoneRecruitmentVMChanged : ICommand
{
    public string MobilePartyId { get; }

    public (string, string, int)[] TroopsInCart { get; }

    public int TotalCost { get; }

    public OnDoneRecruitmentVMChanged(string mobilePartyId, (string, string, int)[] troopsInCart, int totalCost)
    {
        MobilePartyId = mobilePartyId;
        TroopsInCart = troopsInCart;
        TotalCost = totalCost;
    }
}
