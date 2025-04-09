using Common;
using GameInterface.Registry;
using GameInterface.Registry.Auto;
using HarmonyLib;
using Serilog;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Localization;
using static TaleWorlds.CampaignSystem.CampaignBehaviors.CraftingCampaignBehavior;

namespace GameInterface.Services.WeaponDesigns
{
    internal class WeaponDesignRegistry : IAutoRegistry<WeaponDesign>
    {
        ILogger Logger { get; }

        public IEnumerable<MethodBase> Constructors => new MethodBase[] {
        AccessTools.Constructor(typeof(WeaponDesign), new Type[] { typeof(CraftingTemplate), typeof(TextObject), typeof(WeaponDesignElement[]) })
    };

        public IEnumerable<MethodBase> DestroyMethods => Array.Empty<MethodBase>();

        public WeaponDesignRegistry(ILogger logger, IAutoRegistryFactory autoRegistryFactory)
        {
            Logger = logger;

            autoRegistryFactory.RegisterType(this);
        }

        public void RegisterAllObjects(IRegistry<WeaponDesign> registry)
        {
            var dict = Campaign.Current.GetCampaignBehavior<CraftingCampaignBehavior>()._craftedItemDictionary;
            foreach (KeyValuePair<ItemObject, CraftedItemInitializationData> craft in dict)
            {
                var networkId = $"{nameof(WeaponDesign)}_{craft.Key.StringId}";
                if (registry.RegisterExistingObject(networkId, craft.Value.CraftedData) == false)
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
