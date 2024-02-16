using ProtoBuf;
using System;
using System.Collections.Generic;
using System.Text;

namespace GameInterface.Services.MobileParties.Data;

[ProtoContract(SkipConstructor = true)]
public class PartyArmyChangeData
{
    public PartyArmyChangeData(string partyId, string armyId)
    {
        PartyId = partyId;
        ArmyId = armyId;
    }

    [ProtoMember(1)]
    public string PartyId { get; }
    [ProtoMember(2)]
    public string ArmyId { get; }
}
