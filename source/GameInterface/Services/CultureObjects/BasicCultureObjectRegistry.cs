using GameInterface.Services.Registry;
using System;
using TaleWorlds.CampaignSystem;

namespace GameInterface.Services.BasicCultureObjects
{
    internal class CultureObjectRegistry : RegistryBase<CultureObject>
    {
        public CultureObjectRegistry(IRegistryCollection collection) : base(collection) { }

        public override void RegisterAll()
        {

        }

        protected override string GetNewId(CultureObject craft)
        {
            return Guid.NewGuid().ToString();
        }
    }
}
