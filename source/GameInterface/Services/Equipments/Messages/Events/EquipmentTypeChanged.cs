using Common.Messaging;
using TaleWorlds.Core;

namespace GameInterface.Services.Equipments.Messages.Events;

/// <summary>
/// Event from GameInterface for _equipmentType
/// </summary>
public record EquipmentTypeChanged(int EquipmentType, string EquipmentId) : IEvent
{
    public int EquipmentType { get; } = (int)EquipmentType;

    public string EquipmentId { get; } = EquipmentId;
}