using System;
using System.Collections.Generic;
using System.Text;
using Common.Messaging;
using ProtoBuf;

namespace GameInterface.Services.TroopRosters.Messages;
[ProtoContract(SkipConstructor = true)]
public class ApproveChangeOnDoneRecruitmentVM : ICommand
{
    [ProtoMember(1)]
    public string MobilePartyId { get; }
    [ProtoMember(2)]
    public (string, string, int)[] TroopsInCart { get; }
    [ProtoMember(3)]
    public int Gold { get; }

    public ApproveChangeOnDoneRecruitmentVM(string mobilePartyId, (string, string, int)[] troopsInCart, int gold)
    {
        MobilePartyId = mobilePartyId;
        TroopsInCart = troopsInCart;
        Gold = gold;
    }
}
