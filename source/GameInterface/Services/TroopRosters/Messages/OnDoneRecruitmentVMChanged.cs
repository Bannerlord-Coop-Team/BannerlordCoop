using Common.Messaging;
using System;
using System.Collections.Generic;
using System.Text;

namespace GameInterface.Services.TroopRosters.Messages;
public record OnDoneRecruitmentVMChanged : ICommand
{
    public string MobilePartyId { get; }

    public string CharacterId { get; }

    public (string, string, int)[] TroopsInCart { get; }

    public OnDoneRecruitmentVMChanged(string mobilePartyId, (string, string, int)[] troopsInCart)
    {
        MobilePartyId = mobilePartyId;
        TroopsInCart = troopsInCart;
    }
}
