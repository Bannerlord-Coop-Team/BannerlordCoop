using GameInterface.Services.Registry;
using System.Threading;
using static TaleWorlds.CampaignSystem.Siege.SiegeEvent;

namespace GameInterface.Services.SiegeEngines
{
    internal class SiegeEngineConstructionProgressRegistry : RegistryBase<SiegeEngineConstructionProgress>
    {
        private const string SiegeEngineConstructionProgressIdPrefix = "CoopSiegeEngineConstructionProgress";
        private static int InstanceCounter = 0;

        public SiegeEngineConstructionProgressRegistry(IRegistryCollection collection) : base(collection)
        {
        }

        public override void RegisterAll()
        {
        }

        protected override string GetNewId(SiegeEngineConstructionProgress obj)
        {
            return $"{SiegeEngineConstructionProgressIdPrefix}_{Interlocked.Increment(ref InstanceCounter)}";
        }
    }
}