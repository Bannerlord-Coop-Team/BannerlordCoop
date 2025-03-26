using Common;
using GameInterface.Registry.Auto;
using HarmonyLib;
using Serilog;
using System;
using System.Collections.Generic;
using System.Reflection;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.Core;
using static TaleWorlds.CampaignSystem.CampaignBehaviors.CraftingCampaignBehavior;

namespace GameInterface.Services.ItemObjects
{
    internal class WeaponDesignRegistry : IAutoRegistry<WeaponDesign>
    {
        ILogger Logger { get; }
        public WeaponDesignRegistry(ILogger logger, IAutoRegistryFactory autoRegistryFactory)
        {
            Logger = logger;

            autoRegistryFactory.RegisterType(this);
        }

        public IEnumerable<MethodBase> Constructors => AccessTools.GetDeclaredConstructors(typeof(WeaponDesign));

        public IEnumerable<MethodBase> DestroyMethods => Array.Empty<MethodBase>();

        public void RegisterAllObjects(IRegistry<WeaponDesign> registry)
        {
            var dict = Campaign.Current.GetCampaignBehavior<CraftingCampaignBehavior>()._craftedItemDictionary;
            foreach (KeyValuePair<ItemObject, CraftedItemInitializationData> craft in dict)
            {
                var networkId = $"{nameof(WeaponDesign)}_{craft.Key.StringId}";
                if (registry.RegisterNewObject(craft.Value.CraftedData, out var _) == false)
                    Logger.Error($"Unable to register {craft.Value.CraftedData}");
            }
        }

        public void OnClientCreated(WeaponDesign obj, string id)
        {
        }

        public void OnClientDestroyed(WeaponDesign obj, string id)
        {
        }

        public void OnServerCreated(WeaponDesign obj, string id)
        {
        }

        public void OnServerDestroyed(WeaponDesign obj, string id)
        {
        }
    }
}
