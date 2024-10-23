using TaleWorlds.Core;
using static TaleWorlds.CampaignSystem.Siege.SiegeEvent;

namespace E2E.Tests.Util.ObjectBuilders
{
    internal class SiegeEnginesBuilder : IObjectBuilder
    {
        public object Build()
        {
            var siegeEngineType = new SiegeEngineType();
            var prog = new SiegeEngineConstructionProgress(siegeEngineType, 1, 100);
            return new SiegeEnginesContainer(TaleWorlds.Core.BattleSideEnum.Attacker, prog);
        }
    }
}