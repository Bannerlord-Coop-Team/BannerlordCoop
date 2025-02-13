using Common.Messaging;
using TaleWorlds.CampaignSystem.Siege;

namespace GameInterface.Services.SiegeStrategies.Messages;
internal class SiegeStrategyCreated : IEvent
{
    public SiegeStrategy Instance { get; }
    
    public SiegeStrategyCreated(SiegeStrategy instance)
    {
        Instance = instance;
    }
}
