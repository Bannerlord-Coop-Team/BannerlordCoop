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
    internal class WorkshopRegistry : AutoRegistryBase<Workshop>
    {
        public WorkshopRegistry(ILogger logger, IAutoRegistryFactory autoRegistryFactory, IObjectManager objectManager)
            : base(logger, autoRegistryFactory, objectManager)
        {
        }

        public override IEnumerable<MethodBase> Constructors => Array.Empty<MethodBase>();

        public override IEnumerable<MethodBase> DestroyMethods => Array.Empty<MethodBase>();

        public override void RegisterAllObjects()
        {
            foreach (Town town in Town.AllTowns)
            {
                int counter = 1;

                foreach (Workshop workshop in town.Workshops)
                {
                    var networkId = $"{nameof(Workshop)}_{town.StringId}_{counter++}";
                    if (workshop == null) continue;
                    RegisterExistingObject(networkId, workshop);
                }
            }
        }

        public override void OnClientCreated(Workshop obj, string id)
        {
        }

        public override void OnClientDestroyed(Workshop obj, string id)
        {
        }

        public override void OnServerCreated(Workshop obj, string id)
        {
        }

        public override void OnServerDestroyed(Workshop obj, string id)
        {
        }
    }
}