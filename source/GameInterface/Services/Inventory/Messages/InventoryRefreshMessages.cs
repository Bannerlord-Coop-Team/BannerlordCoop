using Common.Messaging;
using ProtoBuf;
using TaleWorlds.CampaignSystem.ViewModelCollection.Inventory;
using TaleWorlds.Core;

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
    public readonly string FromItemRosterId;

    [ProtoMember(2)]
    public readonly string ToItemRosterId;

    [ProtoMember(3)]
    public readonly EquipmentElement EquipmentElement;

    public RefreshOtherInventory(string fromItemRosterId, string toItemRosterId, EquipmentElement equipmentElement)
    {
        FromItemRosterId = fromItemRosterId;
        ToItemRosterId = toItemRosterId;
        EquipmentElement = equipmentElement;
    }
}

[ProtoContract(SkipConstructor = true)]
internal readonly struct RefreshAfterTrade : ICommand
{
    [ProtoMember(1)]
    public readonly string ToItemRosterId;

    [ProtoMember(2)]
    public readonly string FromItemRosterId;

    public RefreshAfterTrade(string toItemRosterId, string fromItemRosterId)
    {
        ToItemRosterId = toItemRosterId;
        FromItemRosterId = fromItemRosterId;
    }
}