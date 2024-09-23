using Common.Messaging;
using ProtoBuf;

namespace GameInterface.Services.Armies.Messages.Lifetime;

[ProtoContract(SkipConstructor = true)]
internal class NetworkCreateBuilding : ICommand
{
    [ProtoMember(1)]
    public string BuildingId { get; set; }
    [ProtoMember(2)]
    public string BuildingTypeId { get; }
    [ProtoMember(3)]
    public string TownId { get; }
    [ProtoMember(4)]
    public float BuildingProgress { get; }
    [ProtoMember(5)]
    public int CurrentLevel { get; }

    public NetworkCreateBuilding(string buildingId, string buildingTypeId, string townId, float buildingProgress, int currentLevel)
    {
        BuildingId = buildingId;
        BuildingTypeId = buildingTypeId;
        TownId = townId;
        BuildingProgress = buildingProgress;
        CurrentLevel = currentLevel;
    }
}
