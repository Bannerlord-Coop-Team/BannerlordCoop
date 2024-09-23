using Common.Messaging;
using GameInterface.Services.Armies.Data;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.Settlements.Buildings;

namespace GameInterface.Services.Armies.Messages.Lifetime;

/// <summary>
/// Event to create a new building.
/// </summary>
public record BuildingCreated : IEvent
{
    public Building Building { get; }
    public BuildingType BuildingType { get; }
    public Town Town { get; }
    public float BuildingProgress { get; }
    public int CurrentLevel { get; }

    public BuildingCreated(Building building, BuildingType buildingType, Town town, float buildingProgress, int currentLevel)
    {
        Building = building;
        BuildingType = buildingType;
        Town = town;
        BuildingProgress = buildingProgress;
        CurrentLevel = currentLevel;
    }
}
