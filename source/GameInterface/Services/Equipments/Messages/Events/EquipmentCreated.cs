using Common.Messaging;
using GameInterface.Services.Equipments.Data;

namespace GameInterface.Services.Equipments.Messages.Events;
internal record EquipmentCreated(EquipmentCreatedData Data) : IEvent
{
    public EquipmentCreatedData Data { get; } = Data;
}