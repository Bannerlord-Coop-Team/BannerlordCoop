using Common.Messaging;
using TaleWorlds.Core;

namespace GameInterface.Services.EquipmentRoster.Messages
{
    internal class EquipmentRosterCreated : IEvent
    {
        public MBEquipmentRoster EquipmentRoster { get; }

        public EquipmentRosterCreated(MBEquipmentRoster equipmentRoster)
        {
            EquipmentRoster = equipmentRoster;
        }
    }
}
