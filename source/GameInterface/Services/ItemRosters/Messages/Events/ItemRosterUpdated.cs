using Common.Messaging;

namespace GameInterface.Services.ItemRosters.Messages.Events
{
    /// <summary>
    /// Called when an item roster is updated.
    /// </summary>
    public class ItemRosterUpdated : IEvent
    {
        public string PartyBaseId { get; }
        public byte[] EquipmentElement { get; }
        public int Number { get; }

        public ItemRosterUpdated(string partyBaseId, byte[] equipmentElement, int number)
        {
            PartyBaseId = partyBaseId;
            EquipmentElement = equipmentElement;
            Number = number;
        }
    }
}
