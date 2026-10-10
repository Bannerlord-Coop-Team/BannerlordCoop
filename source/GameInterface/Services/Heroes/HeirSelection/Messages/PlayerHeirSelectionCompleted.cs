using Common.Messaging;
using TaleWorlds.CampaignSystem;

namespace GameInterface.Services.Heroes.HeirSelection.Messages;

public readonly struct PlayerHeirSelectionCompleted : IEvent
{
    public readonly Hero PlayerHero;

    public PlayerHeirSelectionCompleted(Hero playerHero)
    {
        PlayerHero = playerHero;
    }
}
