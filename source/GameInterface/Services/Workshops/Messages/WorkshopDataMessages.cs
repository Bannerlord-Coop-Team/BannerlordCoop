using Common.Messaging;
using ProtoBuf;
using TaleWorlds.CampaignSystem.Settlements.Workshops;

namespace GameInterface.Services.Workshops.Messages;

public readonly struct NewWorkshopDataAdded : IEvent
{
    public readonly Workshop Workshop;

    public NewWorkshopDataAdded(Workshop workshop)
    {
        Workshop = workshop;
    }
}

public readonly struct WorkshopDataRemoved : IEvent
{
    public readonly Workshop Workshop;

    public WorkshopDataRemoved(Workshop workshop)
    {
        Workshop = workshop;
    }
}

public readonly struct OutputProgressAddedForWarehouse : IEvent
{
    public readonly Workshop Workshop;
    public readonly float ProgressToAdd;

    public OutputProgressAddedForWarehouse(Workshop workshop, float progressToAdd)
    {
        Workshop = workshop;
        ProgressToAdd = progressToAdd;
    }
}

public readonly struct OutputProgressForTownAdded : IEvent
{
    public readonly Workshop Workshop;
    public readonly float ProgressToAdd;

    public OutputProgressForTownAdded(Workshop workshop, float progressToAdd)
    {
        Workshop = workshop;
        ProgressToAdd = progressToAdd;
    }
}

public readonly struct IsGettingInputsFromWarehouseSet : IEvent
{
    public readonly Workshop Workshop;
    public readonly bool IsActive;

    public IsGettingInputsFromWarehouseSet(Workshop workshop, bool isActive)
    {
        Workshop = workshop;
        IsActive = isActive;
    }
}

public readonly struct StockProductionInWarehouseRatioSet : IEvent
{
    public readonly Workshop Workshop;
    public readonly float ProgressToAdd;

    public StockProductionInWarehouseRatioSet(Workshop workshop, float progressToAdd)
    {
        Workshop = workshop;
        ProgressToAdd = progressToAdd;
    }
}

[ProtoContract(SkipConstructor = true)]
public readonly struct AddNewWorkshopData : ICommand
{
    [ProtoMember(1)]
    public readonly string WorkshopId;

    public AddNewWorkshopData(string workshopId)
    {
        WorkshopId = workshopId;
    }
}

[ProtoContract(SkipConstructor = true)]
public readonly struct RemoveWorkshopData : ICommand
{
    [ProtoMember(1)]
    public readonly string WorkshopId;

    public RemoveWorkshopData(string workshopId)
    {
        WorkshopId = workshopId;
    }
}

[ProtoContract(SkipConstructor = true)]
public readonly struct AddOutputProgressForWarehouse : ICommand
{
    [ProtoMember(1)]
    public readonly string WorkshopId;

    [ProtoMember(2)]
    public readonly float ProgressToAdd;

    public AddOutputProgressForWarehouse(string workshopId, float progressToAdd)
    {
        WorkshopId = workshopId;
        ProgressToAdd = progressToAdd;
    }
}

[ProtoContract(SkipConstructor = true)]
public readonly struct AddOutputProgressForTown : ICommand
{
    [ProtoMember(1)]
    public readonly string WorkshopId;

    [ProtoMember(2)]
    public readonly float ProgressToAdd;

    public AddOutputProgressForTown(string workshopId, float progressToAdd)
    {
        WorkshopId = workshopId;
        ProgressToAdd = progressToAdd;
    }
}

[ProtoContract(SkipConstructor = true)]
public readonly struct SetIsGettingInputsFromWarehouse : ICommand
{
    [ProtoMember(1)]
    public readonly string WorkshopId;

    [ProtoMember(2)]
    public readonly bool IsActive;

    public SetIsGettingInputsFromWarehouse(string workshopId, bool isActive)
    {
        WorkshopId = workshopId;
        IsActive = isActive;
    }
}

[ProtoContract(SkipConstructor = true)]
public readonly struct SetIsGettingInputsFromWarehouseClients : ICommand
{
    [ProtoMember(1)]
    public readonly string WorkshopId;

    [ProtoMember(2)]
    public readonly bool IsActive;

    public SetIsGettingInputsFromWarehouseClients(string workshopId, bool isActive)
    {
        WorkshopId = workshopId;
        IsActive = isActive;
    }
}

[ProtoContract(SkipConstructor = true)]
public readonly struct SetStockProductionInWarehouseRatio : ICommand
{
    [ProtoMember(1)]
    public readonly string WorkshopId;

    [ProtoMember(2)]
    public readonly float ProgressToAdd;

    public SetStockProductionInWarehouseRatio(string workshopId, float progressToAdd)
    {
        WorkshopId = workshopId;
        ProgressToAdd = progressToAdd;
    }
}

[ProtoContract(SkipConstructor = true)]
public readonly struct SetStockProductionInWarehouseRatioClients : ICommand
{
    [ProtoMember(1)]
    public readonly string WorkshopId;

    [ProtoMember(2)]
    public readonly float ProgressToAdd;

    public SetStockProductionInWarehouseRatioClients(string workshopId, float progressToAdd)
    {
        WorkshopId = workshopId;
        ProgressToAdd = progressToAdd;
    }
}