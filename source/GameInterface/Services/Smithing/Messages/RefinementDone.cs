using Common.Messaging;
using TaleWorlds.CampaignSystem;
using static TaleWorlds.Core.Crafting;

namespace GameInterface.Services.Smithing.Messages;

public readonly struct DoRefinement : IEvent
{
    public readonly Hero CraftingHero;
    public readonly RefiningFormula RefiningFormula;

    public DoRefinement(
        Hero craftingHero,
        RefiningFormula refiningFormula)
    {
        CraftingHero = craftingHero;
        RefiningFormula = refiningFormula;
    }
}