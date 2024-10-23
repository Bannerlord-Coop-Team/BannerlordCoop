using GameInterface.Services.Registry;
using System.Linq;
using System.Threading;
using TaleWorlds.CampaignSystem;
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
            foreach (var siegeEngineConstructionProgress in Campaign.Current.SiegeEventManager.SiegeEvents
                .Select(siegeEvent => siegeEvent.BesiegerCamp.SiegeEngines.SiegePreparations))
            {
                RegisterNewObject(siegeEngineConstructionProgress, out _);
            }
        }

        protected override string GetNewId(SiegeEngineConstructionProgress obj)
        {
            return $"{SiegeEngineConstructionProgressIdPrefix}_{Interlocked.Increment(ref InstanceCounter)}";
        }
    }
}