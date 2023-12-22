using System.Collections.Generic;
using System.Runtime.CompilerServices;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;

namespace GameInterface.Services.ItemRosters
{
    internal static class ItemRosterLookup
    {
        private static readonly ConditionalWeakTable<ItemRoster, PartyBase> table = new();

        /// <summary>
        /// Adds an entry, or updates an already existing one.
        /// </summary>
        /// <param name="party">PartyBase object</param>
        /// <param name="roster">ItemRoster of the PartyBase</param>
        public static void Set(ItemRoster roster, PartyBase party)
        {
            if (roster == null)
                return;

            if (TryGetValue(roster, out _))
                if (!table.Remove(roster))
                    return;

            table.Add(roster, party);
        }

        /// <summary>
        /// Retrieves an entry.
        /// </summary>
        /// <param name="roster">ItemRoster of a PartyBase</param>
        /// <param name="party">owner PartyBase</param>
        /// <returns>PartyBase or null if not present</returns>
        public static bool TryGetValue(ItemRoster roster, out PartyBase party)
        {
            return table.TryGetValue(roster, out party);
        }
    }
}
