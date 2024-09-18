using GameInterface.Services.Registry;
using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.Settlements.Workshops;
using TaleWorlds.Core;
using TaleWorlds.ObjectSystem;

namespace GameInterface.Services.Workshops
{
    internal class WorkshopRegistry : RegistryBase<Workshop>
    {
        public WorkshopRegistry(IRegistryCollection collection) : base(collection) { }

        public override void RegisterAll()
        {
            var objectManager = MBObjectManager.Instance;

            if (objectManager == null)
            {
                Logger.Error("Unable to register objects when CampaignObjectManager is null");
                return;
            }

            foreach (Town town in Town.AllTowns)
            {
                foreach (Workshop workshop in town.Workshops)
                {
                    RegisterNewObject(workshop, out var _);
                }
             
            }

        }

        protected override string GetNewId(Workshop shop)
        {
            return Guid.NewGuid().ToString();
        }
    }
}