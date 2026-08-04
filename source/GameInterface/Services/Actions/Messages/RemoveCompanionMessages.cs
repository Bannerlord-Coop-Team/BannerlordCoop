using Common.Messaging;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;

namespace GameInterface.Services.Actions.Messages;

public readonly struct CompanionRemovalAttempted : IEvent
{
    public readonly Clan Clan;
    public readonly Hero Companion;
    public readonly RemoveCompanionAction.RemoveCompanionDetail Detail;

    public CompanionRemovalAttempted(Clan clan, Hero companion, RemoveCompanionAction.RemoveCompanionDetail detail)
    {
        Clan = clan;
        Companion = companion;
        Detail = detail;
    }
}
