using Common.Messaging;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Settlements;

namespace GameInterface.Services.Buildings.Messages;

public readonly struct OnSettlementOwnerChanged : IEvent
{
    public readonly Settlement Settlement;
    public readonly Hero NewOwner;

    public OnSettlementOwnerChanged(Settlement settlement, Hero newOwner)
    {
        Settlement = settlement;
        NewOwner = newOwner;
    }
}

public readonly struct BuildingsDailySettlementTick : IEvent
{
    public readonly BuildingsCampaignBehavior BuildingsCampaignBehavior;
    public readonly Settlement Settlement;

    public BuildingsDailySettlementTick(BuildingsCampaignBehavior buildingsCampaignBehavior, Settlement settlement)
    {
        BuildingsCampaignBehavior = buildingsCampaignBehavior;
        Settlement = settlement;
    }
}