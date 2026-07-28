using Common.Messaging;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.Localization;

namespace GameInterface.Services.Smithing.Messages;

public readonly struct BehaviorCraftedWeaponNameSet : IEvent
{
    public readonly CraftingCampaignBehavior CraftingCampaignBehavior;
    public readonly string CraftedWeaponId;
    public readonly TextObject Name;

    public BehaviorCraftedWeaponNameSet(CraftingCampaignBehavior craftingCampaignBehavior, string craftedWeaponId, TextObject name)
    {
        CraftingCampaignBehavior = craftingCampaignBehavior;
        CraftedWeaponId = craftedWeaponId;
        Name = name;
    }
}