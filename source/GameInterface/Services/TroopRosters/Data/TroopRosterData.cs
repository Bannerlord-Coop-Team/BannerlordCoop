using ProtoBuf;
using System.Collections.Generic;
using System.Linq;

namespace GameInterface.Services.TroopRosters.Data;

[ProtoContract(SkipConstructor = true)]
public readonly struct TroopRosterData
{
    [ProtoMember(1)]
    public readonly TroopRosterElementData[] Data;

    public TroopRosterData(IEnumerable<TroopRosterElementData> data)
    {
        Data = data.ToArray();
    }
}