using Common.Messaging;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.Core;

namespace GameInterface.Services.Inventory.Messages;

public readonly struct ItemSlaughtered : IEvent
{
    public readonly ItemRoster TargetItemRoster;
    public readonly EquipmentElement EquipmentElement;
    public readonly int MeatCount;
    public readonly int HideCount;

    public ItemSlaughtered(
        ItemRoster targetItemRoster,
        EquipmentElement equipmentElement,
        int meatCount, int hideCount)
    {
        TargetItemRoster = targetItemRoster;
        EquipmentElement = equipmentElement;
        MeatCount = meatCount;
        HideCount = hideCount;
    }
}
