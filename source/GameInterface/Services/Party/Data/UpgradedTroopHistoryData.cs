using ProtoBuf;
using System.Collections.Generic;

namespace GameInterface.Services.Party.Data;

[ProtoContract(SkipConstructor = true)]
public readonly struct UpgradedTroopHistoryData
{
    [ProtoMember(1)]
    public readonly List<UpgradedTroopHistoryElementData> Data;

    public UpgradedTroopHistoryData(List<UpgradedTroopHistoryElementData> data)
    {
        Data = data;
    }
}