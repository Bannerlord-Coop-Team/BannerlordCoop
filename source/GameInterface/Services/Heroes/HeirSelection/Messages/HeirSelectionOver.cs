using Common.Messaging;
using TaleWorlds.CampaignSystem;

namespace GameInterface.Services.Heroes.HeirSelection.Messages;

public readonly struct HeirSelectionOver : IEvent
{
    public readonly Hero OriginalHero;
    public readonly Hero SelectedHeir;

    public HeirSelectionOver(
        Hero originalHero,
        Hero selectedHeir)
    {
        OriginalHero = originalHero;
        SelectedHeir = selectedHeir;
    }
}