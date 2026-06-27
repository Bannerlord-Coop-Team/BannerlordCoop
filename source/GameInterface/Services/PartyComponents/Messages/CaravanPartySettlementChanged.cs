using Common.Messaging;
using TaleWorlds.CampaignSystem.Party.PartyComponents;
using TaleWorlds.CampaignSystem.Settlements;

namespace GameInterface.Services.PartyComponents.Messages;

internal readonly struct CaravanPartySettlementChanged : IEvent
{
    public readonly CaravanPartyComponent Instance;
    public readonly Settlement Settlement;

    public CaravanPartySettlementChanged(
        CaravanPartyComponent instance,
        Settlement settlement)
    {
        Instance = instance;
        Settlement = settlement;
    }
}
