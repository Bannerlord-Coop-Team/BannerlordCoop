using Common.Messaging;
using TaleWorlds.Core;

namespace GameInterface.Services.Equipments.Messages.Events
{
    internal record EquipmentRemoved : IEvent
    {
        public Equipment battleEquipment;
        public Equipment civilEquipment;

        public EquipmentRemoved(Equipment battleEquipment, Equipment civilEquipment)
        {
            this.battleEquipment = battleEquipment;
            this.civilEquipment = civilEquipment;
        }
    }
}
