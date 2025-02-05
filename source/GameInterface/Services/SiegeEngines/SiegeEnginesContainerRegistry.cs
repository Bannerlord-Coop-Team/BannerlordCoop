using GameInterface.Services.Registry;
using System.Linq;
using System.Threading;
using TaleWorlds.CampaignSystem;
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
            foreach (var siegeEnginesContainer in Campaign.Current.SiegeEventManager.SiegeEvents.Select(siegeEvent => siegeEvent.BesiegerCamp.SiegeEngines))
            {
                RegisterNewObject(siegeEnginesContainer, out _);
            }
        }

        protected override string GetNewId(SiegeEnginesContainer obj)
        {
            return $"{SiegeEnginesContainerIdPrefix}_{Interlocked.Increment(ref InstanceCounter)}";
        }
    }
}