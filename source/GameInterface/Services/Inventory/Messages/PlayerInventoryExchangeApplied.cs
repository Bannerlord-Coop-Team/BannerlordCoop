using Common.Messaging;
using TaleWorlds.CampaignSystem;

namespace GameInterface.Services.Inventory.Messages;

public readonly struct PlayerInventoryExchangeApplied : IEvent
{
    public readonly Hero Hero;

    public PlayerInventoryExchangeApplied(Hero hero)
    {
        Hero = hero;
    }
}
