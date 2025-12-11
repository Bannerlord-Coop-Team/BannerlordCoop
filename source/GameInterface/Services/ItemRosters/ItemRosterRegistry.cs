using GameInterface.Registry;
using GameInterface.Services.ItemRosters.Data;
using System.Linq;
using System.Threading;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.Settlements;

namespace GameInterface.Services.ItemRosters
{
    internal class ItemRosterRegistry : RegistryBase<ItemRoster>
    {
        private const string ItemRosterIdPrefix = "CoopItemRoster";
        private static int ItemRosterCounter = 0;
        public ItemRosterRegistry(IRegistryCollection collection) : base(collection) { }

        public override void RegisterAll()
        {
            var objectManager = Campaign.Current?.CampaignObjectManager;

            if (objectManager == null)
            {
                Logger.Error("Unable to register objects when CampaignObjectManager is null");
                return;
            }

            foreach (MobileParty party in objectManager.MobileParties)
            {
                if (party.ItemRoster == null) continue;

                var networkId = nameof(ItemRoster) + "_" + party.StringId;
                if (TryGetValue<ItemRoster>(networkId, out _)) { }
                else RegisterExistingObject(networkId, party.ItemRoster);
                if (party.Party != null)
                {
                    ItemRosterLookup.Set(party.ItemRoster, party.Party);
                }
            }

            foreach(Settlement settlement in objectManager.Settlements)
            {
                if (settlement.ItemRoster == null) continue;

                var networkId = nameof(ItemRoster) + "_" + settlement.StringId;
                if (TryGetValue<ItemRoster>(networkId, out _)) { }
                else RegisterExistingObject(networkId, settlement.ItemRoster);
                if (settlement.Party != null)
                {
                    ItemRosterLookup.Set(settlement.ItemRoster, settlement.Party);
                }
            }

            var snapshot = RegistrySnapshot.ItemRosterOwners;
            if (snapshot != null)
            {
                foreach (var entry in snapshot)
                {
                    if (string.IsNullOrEmpty(entry?.ItemRosterId)) continue;
                    if (string.IsNullOrEmpty(entry.OwnerPartyId)) continue;
                    if (TryGetValue<ItemRoster>(entry.ItemRosterId, out var roster) == false) continue;

                    PartyBase owner = null;
                    var mp = objectManager.MobileParties.FirstOrDefault(p => p.StringId == entry.OwnerPartyId);
                    if (mp?.Party != null) owner = mp.Party;
                    if (owner == null)
                    {
                        var st = objectManager.Settlements.FirstOrDefault(s => s.StringId == entry.OwnerPartyId);
                        if (st?.Party != null) owner = st.Party;
                    }
                    if (owner != null)
                    {
                        ItemRosterLookup.Set(roster, owner);
                    }
                }
            }
        }

        protected override string GetNewId(ItemRoster itemRoster)
        {
            return $"{ItemRosterIdPrefix}_{Interlocked.Increment(ref ItemRosterCounter)}";
        }
    }
}
