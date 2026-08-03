using Common.Messaging;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;

namespace GameInterface.Services.Smithing.Messages;

public readonly struct DoSmelting : IEvent
{
    public readonly Hero CraftingHero;
    public readonly EquipmentElement EquipmentElement;

    public DoSmelting(Hero craftingHero, EquipmentElement equipmentElement)
    {
        CraftingHero = craftingHero;
        EquipmentElement = equipmentElement;
    }
}