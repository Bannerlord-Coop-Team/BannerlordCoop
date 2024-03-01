using Common.Messaging;
using System;
using System.Collections.Generic;
using System.Text;

namespace GameInterface.Services.TroopRosters.Messages;
public record ProccessRequestOnDoneRecruitmentVM : IEvent
{
    public string MobilePartyId { get; }

    public (string, string, int)[] TroopsInCart { get; }

    public ProccessRequestOnDoneRecruitmentVM(string mobilePartyId, (string, string, int)[] troopsInCart)
    {
        MobilePartyId = mobilePartyId;
        TroopsInCart = troopsInCart;
    }
}
