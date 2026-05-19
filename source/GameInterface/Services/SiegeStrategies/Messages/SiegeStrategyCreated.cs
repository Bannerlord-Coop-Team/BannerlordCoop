using Common.Messaging;
using TaleWorlds.CampaignSystem.Siege;

namespace GameInterface.Services.SiegeStrategies.Messages.Lifetime;

/// <summary>
/// Event to create a new siegestrategy.
/// </summary>
public record SiegeStrategyCreated : IEvent
{
    public SiegeStrategy SiegeStrategy { get; }

    public SiegeStrategyCreated(SiegeStrategy siegeStrategy)
    {
        SiegeStrategy = siegeStrategy;
    }
}