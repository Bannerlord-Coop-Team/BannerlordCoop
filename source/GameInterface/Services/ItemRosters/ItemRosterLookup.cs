using System;
using System.Runtime.CompilerServices;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;

namespace GameInterface.Services.ItemRosters
{
    /// <summary>
    /// Used for looking up PartyBase of a ItemRoster. Useful when patching ItemRoster functions.
    /// </summary>
    internal static class ItemRosterLookup
    {
        private static readonly ConditionalWeakTable<ItemRoster, PartyBase> table = new();

        /// <summary>
        /// Adds an entry, or updates an already existing one.
        /// </summary>
        /// <param name="party">PartyBase object</param>
        /// <param name="roster">ItemRoster of the PartyBase</param>
        /// <exception cref="ArgumentNullException"/>
        public static void Set(ItemRoster roster, PartyBase party)
        {
            if (roster == null)
                throw new ArgumentNullException("ItemRoster must not be null");

            if (TryGetValue(roster, out _))
                table.Remove(roster);

            table.Add(roster, party);
        }

        /// <summary>
        /// Retrieves an entry.
        /// </summary>
        /// <param name="roster">The ItemRoster</param>
        /// <param name="party">The Owner</param>
        /// <returns>PartyBase</returns>
        /// <exception cref="ArgumentNullException"/>
        public static bool TryGetValue(ItemRoster roster, out PartyBase party)
        {
            if (roster == null)
                throw new ArgumentNullException("ItemRoster must not be null");

            return table.TryGetValue(roster, out party);
        }
    }
}
