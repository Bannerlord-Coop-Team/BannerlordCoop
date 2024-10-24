using GameInterface.Services.Registry;
using System.Threading;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements.Buildings;
using TaleWorlds.Core;

namespace GameInterface.Services.ItemObjects
{
    internal class ItemObjectRegistry : RegistryBase<ItemObject>
    {
        private const string ItemObjectIdPrefix = "CoopItemObject";
        private static int InstanceCounter = 0;

        public ItemObjectRegistry(IRegistryCollection collection) : base(collection) { }

        public override void RegisterAll()
        {
            //Are these registered somewhere?
            foreach(ItemObject item in Campaign.Current.AllItems)
            {
                if (RegisterNewObject(item, out var _) == false)
                {
                    Logger.Error($"Unable to register {item}");
                }
            }
        }

        protected override string GetNewId(ItemObject obj)
        {
            return $"{ItemObjectIdPrefix}_{Interlocked.Increment(ref InstanceCounter)}";
        }
    }
}
