using Common.Messaging;
using TaleWorlds.Core;

namespace GameInterface.Services.ItemRosters.Messages.Commands.Internal
{
    internal class PrepareItemRosterUpdated : ICommand
    {
        public string PartyBaseId { get; }
        public EquipmentElement EquipmentElement { get; }
        public int Number { get; }

        public PrepareItemRosterUpdated(string partyBaseId, EquipmentElement equipmentElement, int number)
        {
            PartyBaseId = partyBaseId;
            EquipmentElement = equipmentElement;
            Number = number;
        }
    }
}
