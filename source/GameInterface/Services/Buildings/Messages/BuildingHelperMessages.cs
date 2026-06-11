using Common.Messaging;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.Settlements.Buildings;

namespace GameInterface.Services.Buildings.Messages;

public readonly struct DefaultBuildingChanged : IEvent
{
    public readonly Building NewDefault;
    public readonly Town Town;

    public DefaultBuildingChanged(Building newDefault, Town town)
    {
        NewDefault = newDefault;
        Town = town;
    }
}

public readonly struct CurrentBuildingQueueChanged : IEvent
{
    public readonly List<Building> Buildings;
    public readonly Town Town;

    public CurrentBuildingQueueChanged(List<Building> buildings, Town town)
    {
        Buildings = buildings;
        Town = town;
    }
}

public readonly struct BuildingProcessBoostedWithGold : IEvent
{
    public readonly int Gold;
    public readonly Town Town;
    public readonly Hero Hero;

    public BuildingProcessBoostedWithGold(int gold, Town town, Hero hero)
    {
        Gold = gold;
        Town = town;
        Hero = hero;
    }
}