using Common.Messaging;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.Core;

namespace GameInterface.Services.Inventory.Messages;

public readonly struct TransferAttempted : IEvent
{
    public readonly ItemRoster TargetItemRoster;
    public readonly EquipmentElement EquipmentElement;
    public readonly int Count;

    public TransferAttempted(
        ItemRoster targetItemRoster,
        EquipmentElement equipmentElement,
        int count)
    {
        TargetItemRoster = targetItemRoster;
        EquipmentElement = equipmentElement;
        Count = count;
    }
}
