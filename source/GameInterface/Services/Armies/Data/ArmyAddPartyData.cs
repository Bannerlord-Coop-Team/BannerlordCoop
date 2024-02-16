using ProtoBuf;
using System;
using System.Collections.Generic;
using System.Text;

namespace GameInterface.Services.Armies.Data;
[ProtoContract(SkipConstructor = true)]
public record ArmyAddPartyData
{
    public ArmyAddPartyData(string armyStringId, string partyStringId)
    {
        ArmyStringId = armyStringId;
        PartyStringId = partyStringId;
    }

    [ProtoMember(1)]
    public string ArmyStringId { get; set; }
    [ProtoMember(2)]
    public string PartyStringId { get; set; }

}
