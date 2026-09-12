using Common.Messaging;
using TaleWorlds.CampaignSystem.Settlements;

namespace GameInterface.Services.Buildings.Messages;

public readonly struct RefreshPlayerSettlementManagementVM : IEvent
{
    public readonly Town Town;

    public RefreshPlayerSettlementManagementVM(Town town)
    {
        Town = town;
    }
}
