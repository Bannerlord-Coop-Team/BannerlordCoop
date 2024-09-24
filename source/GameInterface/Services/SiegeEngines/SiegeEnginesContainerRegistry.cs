using GameInterface.Services.Registry;
using System;
using static TaleWorlds.CampaignSystem.Siege.SiegeEvent;

namespace GameInterface.Services.SiegeEngines
{
    internal class SiegeEnginesContainerRegistry : RegistryBase<SiegeEnginesContainer>
    {
        public SiegeEnginesContainerRegistry(IRegistryCollection collection) : base(collection)
        {
        }

        public override void RegisterAll()
        {
        }

        protected override string GetNewId(SiegeEnginesContainer obj)
        {
            return Guid.NewGuid().ToString();
        }
    }
}