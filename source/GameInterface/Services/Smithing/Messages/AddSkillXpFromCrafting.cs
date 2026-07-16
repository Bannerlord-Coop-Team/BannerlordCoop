using Common.Messaging;
using TaleWorlds.CampaignSystem;

namespace GameInterface.Services.Smithing.Messages;

public readonly struct AddSkillXpFromCrafting : IEvent
{
    public readonly Hero CraftingHero;
    public readonly float Xp;

    public AddSkillXpFromCrafting(Hero craftingHero, float xp)
    {
        CraftingHero = craftingHero;
        Xp = xp;
    }
}
