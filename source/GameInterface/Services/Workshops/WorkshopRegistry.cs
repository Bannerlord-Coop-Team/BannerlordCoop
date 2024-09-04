using GameInterface.Services.Registry;
using ProtoBuf.Meta;
using System;
using System.Collections.Generic;
using System.Text;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.Settlements.Workshops;

namespace GameInterface.Services.Workshops
{
    internal class WorkshopRegistry : RegistryBase<Workshop>
    {
        public WorkshopRegistry(IRegistryCollection collection) : base(collection) { }

        public override void RegisterAll()
        {
            foreach(Town town in Town.AllTowns)
            {
                foreach(Workshop workshop in town.Workshops)
                {
                    RegisterNewObject(workshop, out var _);
                }
            }
        }

        protected override string GetNewId(Workshop party)
        {
            return Guid.NewGuid().ToString();
        }
    }
}
