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

    public BuildingCreated(Building building)
    {
        Building = building;
    }
}
