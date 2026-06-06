using Common.Messaging;
using GameInterface.Utils.LocalEvents;
using TaleWorlds.Core;

namespace GameInterface.Services.Equipments.Messages.Events;
internal record ItemSlotsArrayUpdated : GenericArrayChangedEvent<Equipment, EquipmentElement>
{
    public ItemSlotsArrayUpdated(Equipment instance, EquipmentElement value, int index) : base(instance, value, index)
    {
    }
}
