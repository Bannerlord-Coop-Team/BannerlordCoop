using GameInterface.Services.Registry;
using System.Threading;
using static TaleWorlds.CampaignSystem.Siege.SiegeEvent;

namespace GameInterface.Services.SiegeEngines
{
    internal class SiegeEnginesContainerRegistry : RegistryBase<SiegeEnginesContainer>
    {
        private const string SiegeEnginesContainerIdPrefix = "CoopSiegeEnginesContainer";
        private static int InstanceCounter = 0;

        public SiegeEnginesContainerRegistry(IRegistryCollection collection) : base(collection)
        {
        }

        public override void RegisterAll()
        {
        }

        protected override string GetNewId(SiegeEnginesContainer obj)
        {
            return $"{SiegeEnginesContainerIdPrefix}_{Interlocked.Increment(ref InstanceCounter)}";
        }
    }
}