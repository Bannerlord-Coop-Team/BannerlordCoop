using Common.Messaging;
using ProtoBuf;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using static TaleWorlds.Core.Crafting;

namespace GameInterface.Services.Smithing.Messages;

public record RefinementDone : IEvent
{
    public CraftingCampaignBehavior CraftingCampaignBehavior;
    public Hero CraftingHero;
    public RefiningFormula RefiningFormula;

    public RefinementDone(CraftingCampaignBehavior craftingCampaignBehavior, Hero craftingHero, RefiningFormula refiningFormula)
    {
        CraftingCampaignBehavior = craftingCampaignBehavior;
        CraftingHero = craftingHero;
        RefiningFormula = refiningFormula;
    }
}