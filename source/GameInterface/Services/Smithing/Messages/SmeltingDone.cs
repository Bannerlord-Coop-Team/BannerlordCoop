using Common.Messaging;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.Core;

namespace GameInterface.Services.Smithing.Messages;

public record SmeltingDone : IEvent
{
    public CraftingCampaignBehavior CraftingCampaignBehavior;
    public Hero CraftingHero;
    public EquipmentElement EquipmentElement;

    public SmeltingDone(CraftingCampaignBehavior craftingCampaignBehavior, Hero craftingHero, EquipmentElement equipmentElement)//, ItemRoster itemRoster)
    {
        CraftingCampaignBehavior = craftingCampaignBehavior;
        CraftingHero = craftingHero;
        EquipmentElement = equipmentElement;
    }
}