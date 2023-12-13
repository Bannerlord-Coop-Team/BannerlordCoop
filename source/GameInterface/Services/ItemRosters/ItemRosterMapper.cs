using System;
using System.Runtime.CompilerServices;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;

namespace GameInterface.Services.ItemRosters
{
    internal interface IItemRosterMapper
    {
        /// <summary>
        /// Adds an entry, or updates an already existing one.
        /// </summary>
        /// <param name="party">PartyBase object.</param>
        /// <param name="roster">ItemRoster of the PartyBase.</param>
        void Set(ItemRoster roster, PartyBase party);

        /// <summary>
        /// Retrieves an entry.
        /// </summary>
        /// <param name="roster">ItemRoster object</param>
        /// <returns>PartyBase or null if not present</returns>
        PartyBase Get(ItemRoster roster);

    }

    internal class ItemRosterMapper : IItemRosterMapper
    {
        public static readonly ItemRosterMapper Instance = new();

        private readonly ConditionalWeakTable<ItemRoster, PartyBase> table;

        public ItemRosterMapper()
        {
            table = new();
        }

        public PartyBase Get(ItemRoster roster)
        {
            table.TryGetValue(roster, out PartyBase pb);
            return pb;
        }

        public void Set(ItemRoster roster, PartyBase party)
        {
            if (roster == null)
                return;

            if (Get(roster) != null)
                table.Remove(roster);

            try
            {
                table.Add(roster, party);
            }
            catch (ArgumentException e)
            {
                //if (table.Remove(roster))
                //    Set(party, roster); throws exceptions even though they are handled, TODO: fix, then remove if (Get(roster) != null)
            }
        }
    }
}
