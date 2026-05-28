using Common.Messaging;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.Localization;

namespace GameInterface.Services.Smithing.Messages;

public record BehaviorCraftedWeaponNameSet : IEvent
{
    public CraftingCampaignBehavior CraftingCampaignBehavior;
    public string CraftedWeaponId;
    public TextObject Name;

    public BehaviorCraftedWeaponNameSet(CraftingCampaignBehavior craftingCampaignBehavior, string craftedWeaponId, TextObject name)
    {
        CraftingCampaignBehavior = craftingCampaignBehavior;
        CraftedWeaponId = craftedWeaponId;
        Name = name;
    }
}