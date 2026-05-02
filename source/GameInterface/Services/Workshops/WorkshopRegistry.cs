using GameInterface.Registry;
using GameInterface.Registry.Auto;
using GameInterface.Services.ObjectManager;
using Serilog;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
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

        public IEnumerable<MethodBase> Constructors => Array.Empty<MethodBase>();

        public IEnumerable<MethodBase> DestroyMethods => Array.Empty<MethodBase>();

        public void RegisterAllObjects(IObjectManager objectManager)
        {
            foreach (Town town in Town.AllTowns)
            {
                int counter = 1;

                foreach (Workshop workshop in town.Workshops)
                {
                    var networkId = $"{nameof(Workshop)}_{town.StringId}_{counter++}";
                    objectManager.AddExisting(networkId, workshop);
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