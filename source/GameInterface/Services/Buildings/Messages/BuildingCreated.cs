using Common.Messaging;
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
