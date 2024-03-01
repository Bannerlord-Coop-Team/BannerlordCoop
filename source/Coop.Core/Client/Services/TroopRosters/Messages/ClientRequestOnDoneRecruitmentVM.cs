using Common.Messaging;
using ProtoBuf;
using System;
using System.Collections.Generic;
using System.Text;

namespace Coop.Core.Client.Services.TroopRosters.Messages;

[ProtoContract(SkipConstructor = true)]
public record ClientRequestOnDoneRecruitmentVM : IEvent
{
    [ProtoMember(1)]
    public string MobilePartyId { get; }
    [ProtoMember(2)]
    public (string, string, int)[] TroopsInCart { get; }

    public ClientRequestOnDoneRecruitmentVM(string mobilePartyId, (string, string, int)[] troopsInCart)
    {
        MobilePartyId = mobilePartyId;
        TroopsInCart = troopsInCart;
    }
}
