using Common.Messaging;
using TaleWorlds.CampaignSystem;

namespace GameInterface.Services.Heroes.HeirSelection.Messages;

public readonly struct PlayerHeirSelectionRequested : IEvent
{
    public readonly Hero PlayerHero;

    public PlayerHeirSelectionRequested(Hero playerHero)
    {
        PlayerHero = playerHero;
    }
}
