using ProtoBuf;
using System.Collections.Generic;

namespace GameInterface.Services.TroopRosters.Data;

[ProtoContract(SkipConstructor = true)]
public class TroopRosterOrderData
{
    [ProtoMember(1)]
    public readonly Dictionary<int, string> IndexCharacterIds;

    public TroopRosterOrderData(Dictionary<int, string> indexCharacterIds)
    {
        IndexCharacterIds = indexCharacterIds;
    }
}
