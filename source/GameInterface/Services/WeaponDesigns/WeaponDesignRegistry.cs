using GameInterface.Services.Registry;
using System.Collections.Generic;
using System.Threading;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.Core;
using static TaleWorlds.CampaignSystem.CampaignBehaviors.CraftingCampaignBehavior;

namespace GameInterface.Services.ItemObjects
{
    internal class WeaponDesignRegistry : RegistryBase<WeaponDesign>
    {
        private const string ItemObjectIdPrefix = "CoopWeaponDesign";
        private static int InstanceCounter = 0;

        public WeaponDesignRegistry(IRegistryCollection collection) : base(collection) { }

        public override void RegisterAll()
        {
            var dict = Campaign.Current.GetCampaignBehavior<CraftingCampaignBehavior>()._craftedItemDictionary;
            foreach (KeyValuePair<ItemObject, CraftedItemInitializationData> craft in dict)
            {
                if (RegisterNewObject(craft.Value.CraftedData, out var _) == false)
                {
                    Logger.Error($"Unable to register {craft.Value.CraftedData}");
                }
            }
        }

        protected override string GetNewId(WeaponDesign obj)
        {
            return $"{ItemObjectIdPrefix}_{Interlocked.Increment(ref InstanceCounter)}";
        }
    }
}
