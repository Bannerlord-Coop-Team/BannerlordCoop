using GameInterface.Services.Registry;
using System;
using System.Collections.Generic;
using System.Text;
using TaleWorlds.CampaignSystem.Siege;
using static TaleWorlds.CampaignSystem.Siege.SiegeEvent;

namespace GameInterface.Services.SiegeEngines
{
    internal class SiegeEngineConstructionProgressRegistry : RegistryBase<SiegeEngineConstructionProgress>
    {
        public SiegeEngineConstructionProgressRegistry(IRegistryCollection collection) : base(collection) { }

        public override void RegisterAll()
        {

        }

        protected override string GetNewId(SiegeEngineConstructionProgress obj)
        {
            return Guid.NewGuid().ToString();
        }
    }
}
