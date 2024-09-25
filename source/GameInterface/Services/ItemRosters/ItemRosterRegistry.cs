using GameInterface.Services.Registry;
using System.Threading;
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
            foreach(Settlement settlement in Settlement.All)
            {
                RegisterNewObject(settlement, out _);
            }
        }

        protected override string GetNewId(ItemRoster itemRoster)
        {
            return $"{ItemRosterIdPrefix}_{Interlocked.Increment(ref ItemRosterCounter)}";
        }
    }
}
