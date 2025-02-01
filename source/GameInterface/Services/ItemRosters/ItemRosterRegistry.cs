using GameInterface.Services.Registry;
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

                RegisterNewObject(party.ItemRoster, out var _);
            }

            foreach(Settlement settlement in objectManager.Settlements)
            {
                if (settlement.ItemRoster == null) continue;

                RegisterNewObject(settlement.ItemRoster, out var _);
            }
        }

        protected override string GetNewId(ItemRoster itemRoster)
        {
            return $"{ItemRosterIdPrefix}_{Interlocked.Increment(ref ItemRosterCounter)}";
        }
    }
}
