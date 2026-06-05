using ProtoBuf;
using System.Collections.Generic;

namespace GameInterface.Services.TroopRosters.Data;

[ProtoContract(SkipConstructor = true)]
public readonly struct TroopRosterData
{
    [ProtoMember(1)]
    public readonly List<TroopRosterElementData> Data;

    public TroopRosterData(List<TroopRosterElementData> data)
    {
        Data = data;
    }
}