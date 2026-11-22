using Common.Messaging;
using TaleWorlds.CampaignSystem;

namespace GameInterface.Services.Heroes.HeirSelection.Messages;

public readonly struct HeirSelectedForRetirement : IEvent
{
    public readonly Hero OriginalHero;
    public readonly Hero SelectedHeir;

    public HeirSelectedForRetirement(Hero originalHero, Hero selectedHeir)
    {
        OriginalHero = originalHero;
        SelectedHeir = selectedHeir;
    }
}
