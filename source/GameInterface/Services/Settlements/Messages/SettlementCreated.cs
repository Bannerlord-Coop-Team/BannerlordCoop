using Common.Messaging;
using TaleWorlds.CampaignSystem.Settlements;

namespace GameInterface.Services.Settlements.Messages;
internal class SettlementCreated : IEvent
{
    public Settlement Instance { get; }

    public SettlementCreated(Settlement instance)
    {
        Instance = instance;
    }
}
