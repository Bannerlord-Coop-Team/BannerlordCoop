using Common.Logging.Attributes;
using Common.Messaging;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;

namespace GameInterface.Services.ItemRosters.Messages
{
    /// <summary> 
    /// Called when an ItemRoster is updated.
    /// </summary>
    [BatchLogMessage]
    public readonly struct ItemRosterUpdated : IEvent
    {
        public readonly PartyBase PartyBase;
        public readonly ItemObject Item;
        public readonly ItemModifier ItemModifier;
        public readonly int Amount;

        public ItemRosterUpdated(
            PartyBase partyBase,
            ItemObject item,
            ItemModifier itemModifier,
            int amount)
        {
            PartyBase = partyBase;
            Item = item;
            ItemModifier = itemModifier;
            Amount = amount;
        }
    }
}
