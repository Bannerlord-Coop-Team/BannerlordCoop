using System;
using System.Collections.Generic;
using System.Reflection;
using Common;
using GameInterface.Registry.Auto;
using HarmonyLib;
using Serilog;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.Settlements.Workshops;
using TaleWorlds.ObjectSystem;

namespace GameInterface.Services.Workshops
{
    internal class WorkshopRegistry : IAutoRegistry<Workshop>
    {
        ILogger Logger { get; }
        public WorkshopRegistry(ILogger logger, IAutoRegistryFactory autoRegistryFactory)
        {
            Logger = logger;

            autoRegistryFactory.RegisterType(this);
        }

        public IEnumerable<MethodBase> Constructors => new MethodBase[] {
        AccessTools.Constructor(typeof(Workshop), new Type[] { typeof(Settlement), typeof(string) })
    };

        public IEnumerable<MethodBase> DestroyMethods => Array.Empty<MethodBase>();

        public void RegisterAllObjects(IRegistry<Workshop> registry)
        {
            var objectManager = MBObjectManager.Instance;

            if (objectManager == null)
            {
                Logger.Error("Unable to register objects when CampaignObjectManager is null");
                return;
            }

            foreach (Town town in Town.AllTowns)
            {
                int counter = 1;

                foreach (Workshop workshop in town.Workshops)
                {
                    var networkId = $"{nameof(Workshop)}_{town.StringId}_{counter++}";
                    if (registry.RegisterExistingObject(networkId, workshop) == false)
                        Logger.Error($"Unable to register {workshop}");
                }
            }
        }

        public void OnClientCreated(Workshop obj, string id)
        {
            
        }

        public void OnClientDestroyed(Workshop obj, string id)
        {
            
        }

        public void OnServerCreated(Workshop obj, string id)
        {
            
        }

        public void OnServerDestroyed(Workshop obj, string id)
        {
            
        }
    }
}