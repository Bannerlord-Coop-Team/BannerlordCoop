using Common;
using GameInterface.Registry.Auto;
using HarmonyLib;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using static TaleWorlds.CampaignSystem.Siege.SiegeEvent;

namespace GameInterface.Services.SiegeEngines
{
    internal class SiegeEnginesContainerRegistry : IAutoRegistry<SiegeEnginesContainer>
    {
        ILogger Logger { get; }

        public IEnumerable<MethodBase> Constructors => new MethodBase[]
        {
            AccessTools.Constructor(typeof(SiegeEnginesContainer), new Type[] { typeof(BattleSideEnum), typeof(SiegeEngineConstructionProgress) })
        };

        public IEnumerable<MethodBase> DestroyMethods => Array.Empty<MethodBase>();

        public SiegeEnginesContainerRegistry(ILogger logger, IAutoRegistryFactory autoRegistryFactory)
        {
            Logger = logger;

            autoRegistryFactory.RegisterType(this);
        }

        public void RegisterAllObjects(IRegistry<SiegeEnginesContainer> registry)
        {
            foreach (var siegeEnginesContainer in Campaign.Current.SiegeEventManager.SiegeEvents.Select(siegeEvent => siegeEvent.BesiegerCamp.SiegeEngines))
            {
                registry.RegisterNewObject(siegeEnginesContainer, out _);
            }
        }

        public void OnClientCreated(SiegeEnginesContainer obj, string id)
        {
        }

        public void OnClientDestroyed(SiegeEnginesContainer obj, string id)
        {
        }

        public void OnServerCreated(SiegeEnginesContainer obj, string id)
        {
        }

        public void OnServerDestroyed(SiegeEnginesContainer obj, string id)
        {
        }
    }
}