using Common.Messaging;
using TaleWorlds.CampaignSystem.Inventory;
using TaleWorlds.CampaignSystem.Roster;

namespace GameInterface.Services.Inventory.Messages;

public readonly struct ResetRosters : IEvent
{
    public readonly ItemRoster TargetItemRoster1;
    public readonly ItemRoster BackupItemRoster1;
    public readonly ItemRoster TargetItemRoster2;
    public readonly ItemRoster BackupItemRoster2;
    public readonly InventoryLogic InventoryLogic;
    public readonly bool FromCancel;

    public ResetRosters(
        ItemRoster targetItemRoster1,
        ItemRoster backupItemRoster1,
        ItemRoster targetItemRoster2,
        ItemRoster backupItemRoster2,
        InventoryLogic inventoryLogic,
        bool fromCancel)
    {
        TargetItemRoster1 = targetItemRoster1;
        BackupItemRoster1 = backupItemRoster1;
        TargetItemRoster2 = targetItemRoster2;
        BackupItemRoster2 = backupItemRoster2;
        InventoryLogic = inventoryLogic;
        FromCancel = fromCancel;
    }
}
