using Common.Messaging;
using TaleWorlds.CampaignSystem;

namespace GameInterface.Services.Companions.Messages;

public readonly struct AdjustCompanionsEquipment : IEvent
{
    public readonly Hero CompanionHero;

    public AdjustCompanionsEquipment(Hero companionHero)
    {
        CompanionHero = companionHero;
    }
}
