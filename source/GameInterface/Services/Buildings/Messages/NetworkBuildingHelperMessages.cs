using Common.Messaging;
using ProtoBuf;
using System.Collections.Generic;

namespace GameInterface.Services.Buildings.Messages;

[ProtoContract(SkipConstructor = true)]
internal readonly struct ChangeDefaultBuilding : ICommand
{
    [ProtoMember(1)]
    public readonly string NewDefaultId;

    [ProtoMember(2)]
    public readonly string TownId;

    public ChangeDefaultBuilding(string newDefaultId, string townId)
    {
        NewDefaultId = newDefaultId;
        TownId = townId;
    }
}

[ProtoContract(SkipConstructor = true)]
public readonly struct ChangeCurrentBuildingQueue : IEvent
{
    [ProtoMember(1)]
    public readonly List<string> BuildingIds;

    [ProtoMember(2)]
    public readonly string TownId;

    public ChangeCurrentBuildingQueue(List<string> buildingIds, string townId)
    {
        BuildingIds = buildingIds;
        TownId = townId;
    }
}

[ProtoContract(SkipConstructor = true)]
public readonly struct BoostBuildingProcessWithGold : IEvent
{
    [ProtoMember(1)]
    public readonly int Gold;

    [ProtoMember(2)]
    public readonly string TownId;

    [ProtoMember(3)]
    public readonly string HeroId;

    public BoostBuildingProcessWithGold(int gold, string townId, string heroId)
    {
        Gold = gold;
        TownId = townId;
        HeroId = heroId;
    }
}
