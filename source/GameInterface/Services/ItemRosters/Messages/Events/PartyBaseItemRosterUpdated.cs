using Common.Messaging;

namespace GameInterface.Services.ItemRosters.Messages.Events
{
    /// <summary>
    /// Called when an item roster is updated.
    /// </summary>
    public class PartyBaseItemRosterUpdated : IEvent
    {
        public string PartyBaseId { get; }
        public byte[] EquipmentElement { get; }
        public int Number { get; }

        public PartyBaseItemRosterUpdated(string partyBaseId, byte[] equipmentElement, int number)
        {
            PartyBaseId = partyBaseId;
            EquipmentElement = equipmentElement;
            Number = number;
        }
    }
}
