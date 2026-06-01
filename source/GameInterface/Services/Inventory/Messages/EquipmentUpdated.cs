using Common.Messaging;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using static TaleWorlds.Core.Equipment;

namespace GameInterface.Services.Inventory.Messages;

public readonly struct EquipmentUpdated : IEvent
{
    public readonly Hero Hero;
    public readonly EquipmentType EquipmentType;
    public readonly EquipmentElement EquipmentElement;
    public readonly EquipmentIndex EquipmentIndex;

    public EquipmentUpdated(
        Hero hero,
        EquipmentType equipmentType,
        EquipmentElement equipmentElement,
        EquipmentIndex equipmentIndex)
    {
        Hero = hero;
        EquipmentType = equipmentType;
        EquipmentElement = equipmentElement;
        EquipmentIndex = equipmentIndex;
    }
}