using Common.Messaging;
using LiteNetLib;
using ProtoBuf;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.Settlements.Workshops;
using TaleWorlds.Core;

namespace GameInterface.Services.Workshops.Messages;

public readonly struct WorkshopOwnerChanged : IEvent
{
    public readonly Workshop Workshop;
    public readonly Hero OldOwner;

    public WorkshopOwnerChanged(Workshop workshop, Hero oldOwner)
    {
        Workshop = workshop;
        OldOwner = oldOwner;
    }
}

public readonly struct OutputProducedToWarehouse : IEvent
{
    public readonly Workshop Workshop;
    public readonly EquipmentElement OutputItem;

    public OutputProducedToWarehouse(Workshop workshop, EquipmentElement outputItem)
    {
        Workshop = workshop;
        OutputItem = outputItem;
    }
}

public readonly struct InputConsumedFromWarehouse : IEvent
{
    public readonly Workshop Workshop;
    public readonly ItemCategory ProductionInput;
    public readonly int InputCount;

    public InputConsumedFromWarehouse(Workshop workshop, ItemCategory productionInput, int inputCount)
    {
        Workshop = workshop;
        ProductionInput = productionInput;
        InputCount = inputCount;
    }
}

public readonly struct WarehouseRosterManaged : IEvent
{
    public readonly NetPeer NetPeer;
    public readonly Hero Hero;
    public readonly Settlement Settlement;
    public readonly ItemRosterElement[] NewWarehouseRosterData;

    public WarehouseRosterManaged(NetPeer netPeer, Hero hero, Settlement settlement, ItemRosterElement[] newWarehouseRosterData)
    {
        NetPeer = netPeer;
        Hero = hero;
        Settlement = settlement;
        NewWarehouseRosterData = newWarehouseRosterData;
    }
}

[ProtoContract(SkipConstructor = true)]
internal readonly struct ChangeWorkshopOwner : ICommand
{
    [ProtoMember(1)]
    public readonly string WorkshopId;

    [ProtoMember(2)]
    public readonly string OldOwnerId;

    public ChangeWorkshopOwner(string workshopId, string oldOwnerId)
    {
        WorkshopId = workshopId;
        OldOwnerId = oldOwnerId;
    }
}

[ProtoContract(SkipConstructor = true)]
public readonly struct ProduceOutputToWarehouse : ICommand
{
    [ProtoMember(1)]
    public readonly string WorkshopId;

    [ProtoMember(2)]
    public readonly EquipmentElement OutputItem;

    public ProduceOutputToWarehouse(string workshopId, EquipmentElement outputItem)
    {
        WorkshopId = workshopId;
        OutputItem = outputItem;
    }
}

[ProtoContract(SkipConstructor = true)]
public readonly struct ConsumeInputFromWarehouse : ICommand
{
    [ProtoMember(1)]
    public readonly string WorkshopId;

    [ProtoMember(2)]
    public readonly int InputCount;

    [ProtoMember(3)]
    public readonly string ItemId;

    public ConsumeInputFromWarehouse(string workshopId, int inputCount, string itemId)
    {
        WorkshopId = workshopId;
        InputCount = inputCount;
        ItemId = itemId;
    }
}

[ProtoContract(SkipConstructor = true)]
public readonly struct ManageWarehouseRoster : ICommand
{
    [ProtoMember(1)]
    public readonly string SettlementId;

    [ProtoMember(2)]
    public readonly ItemRosterElement[] NewWarehouseRosterData;

    public ManageWarehouseRoster(string settlementId, ItemRosterElement[] newWarehouseRosterData)
    {
        SettlementId = settlementId;
        NewWarehouseRosterData = newWarehouseRosterData;
    }
}