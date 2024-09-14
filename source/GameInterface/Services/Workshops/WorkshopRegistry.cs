using GameInterface.Services.Registry;
using System;
using TaleWorlds.CampaignSystem.Settlements.Workshops;

namespace GameInterface.Services.Workshops
{
    internal class WorkshopRegistry : RegistryBase<Workshop>
    {
        public WorkshopRegistry(IRegistryCollection collection) : base(collection) { }

        public override void RegisterAll()
        {
            //Not needed
        }

        protected override string GetNewId(Workshop shop)
        {
            return Guid.NewGuid().ToString();
        }
    }
}