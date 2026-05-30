using Common.Messaging;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.Core;

namespace GameInterface.Services.Inventory.Messages;

public readonly struct TransferAttempted : IEvent
{
    public readonly ItemRoster FromItemRoster;
    public readonly ItemRoster ToItemRoster;
    public readonly EquipmentElement EquipmentElement;
    public readonly int Count;

    public TransferAttempted(
        ItemRoster fromItemRoster,
        ItemRoster toItemRoster,
        EquipmentElement equipmentElement,
        int count)
    {
        FromItemRoster = fromItemRoster;
        ToItemRoster = toItemRoster;
        EquipmentElement = equipmentElement;
        Count = count;
    }
}