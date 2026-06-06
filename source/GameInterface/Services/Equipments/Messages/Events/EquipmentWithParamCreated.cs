using Common.Messaging;
using TaleWorlds.Core;

namespace GameInterface.Services.Equipments.Messages.Events;
internal record EquipmentWithParamCreated(Equipment Data, Equipment Param) : IEvent
{
    public Equipment Data { get; } = Data;
    public Equipment Param { get; } = Param;

}
