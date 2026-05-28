using Common.Messaging;
using ProtoBuf;
using TaleWorlds.CampaignSystem.ViewModelCollection.Inventory;

namespace GameInterface.Services.Inventory.Messages;

public readonly struct InventoryVMCreated : IEvent
{
    public readonly SPInventoryVM InventoryVM;

    public InventoryVMCreated(SPInventoryVM inventoryVM)
    {
        InventoryVM = inventoryVM;
    }
}

[ProtoContract(SkipConstructor = true)]
internal readonly struct RefreshOtherInventory : ICommand
{
    [ProtoMember(1)]
    public readonly string ItemRosterId;

    public RefreshOtherInventory(string itemRosterId)
    {
        ItemRosterId = itemRosterId;
    }
}