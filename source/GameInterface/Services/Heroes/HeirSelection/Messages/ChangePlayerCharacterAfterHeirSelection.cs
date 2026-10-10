using Common.Messaging;
using TaleWorlds.CampaignSystem;

namespace GameInterface.Services.Heroes.HeirSelection.Messages;

public readonly struct ChangePlayerCharacterAfterHeirSelection : IEvent
{
    public readonly Hero OriginalHero;
    public readonly Hero Heir;

    public ChangePlayerCharacterAfterHeirSelection(
        Hero originalHero,
        Hero heir)
    {
        OriginalHero = originalHero;
        Heir = heir;
    }
}
