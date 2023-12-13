using Common.Messaging;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.Core;

namespace GameInterface.Services.ItemRosters.Messages.Events
{
    /// <summary>
    /// Should be called when an item roster is updated.
    /// </summary>
    public class ItemRosterUpdated : IEvent
    {
        public ItemRoster ItemRoster { get; }
        public PartyBase PartyBase { get; }
        public EquipmentElement EquipmentElement { get; }
        public int Number { get; }

        public ItemRosterUpdated(ItemRoster roster, PartyBase pb, EquipmentElement equipmentElement, int number)
        {
            ItemRoster = roster;
            PartyBase = pb; 
            EquipmentElement = equipmentElement;
            Number = number;
        }
    }
}
