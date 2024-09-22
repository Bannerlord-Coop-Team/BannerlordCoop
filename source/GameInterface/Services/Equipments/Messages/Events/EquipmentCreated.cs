using Common.Messaging;
using GameInterface.Services.Equipments.Data;
using TaleWorlds.Core;

namespace GameInterface.Services.Equipments.Messages.Events;
internal record EquipmentCreated(Equipment Data, Equipment Param = null, bool? IsCivilian = null) : IEvent
{
    public Equipment Data { get; } = Data;
    public Equipment Param { get; } = Param;
    
    public bool? IsCivilian { get; } = IsCivilian;
}
