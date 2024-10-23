using GameInterface.Services.Registry;
using System.Threading;
using TaleWorlds.CampaignSystem.Roster;

namespace GameInterface.Services.ItemRosters
{
    internal class ItemRosterRegistry : RegistryBase<ItemRoster>
    {
        private const string ItemRosterIdPrefix = "CoopItemRoster";
        private static int ItemRosterCounter = 0;
        public ItemRosterRegistry(IRegistryCollection collection) : base(collection) { }

        public override void RegisterAll()
        {
            //TODO
        }

        protected override string GetNewId(ItemRoster itemRoster)
        {
            return $"{ItemRosterIdPrefix}_{Interlocked.Increment(ref ItemRosterCounter)}";
        }
    }
}
