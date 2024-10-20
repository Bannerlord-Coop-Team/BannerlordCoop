using Common.Messaging;
using TaleWorlds.Core;

namespace GameInterface.Services.Equipments.Messages.Events;
internal record EquipmentCreated(Equipment Data, Equipment Param = null) : IEvent
{
    public Equipment Data { get; } = Data;
    public Equipment Param { get; } = Param;
    

}
