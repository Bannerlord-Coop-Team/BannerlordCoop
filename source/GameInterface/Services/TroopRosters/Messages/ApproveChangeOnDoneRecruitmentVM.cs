using System;
using System.Collections.Generic;
using System.Text;

namespace GameInterface.Services.TroopRosters.Messages;
public class ApproveChangeOnDoneRecruitmentVM
{
    public string MobilePartyId { get; }

    public (string, string, int)[] TroopsInCart { get; }

    public int Gold { get; }

    public ApproveChangeOnDoneRecruitmentVM(string mobilePartyId, (string, string, int)[] troopsInCart, int gold)
    {
        MobilePartyId = mobilePartyId;
        TroopsInCart = troopsInCart;
        Gold = gold;
    }
}
