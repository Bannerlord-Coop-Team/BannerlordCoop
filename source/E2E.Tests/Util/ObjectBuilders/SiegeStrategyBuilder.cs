using TaleWorlds.CampaignSystem.Siege;

namespace E2E.Tests.Util.ObjectBuilders
{
    internal class SiegeStrategyBuilder : IObjectBuilder
    {
        public object Build()
        {
            var strategies = SiegeStrategy.All;
            int rndIndex = new Random().Next(strategies.Count);
            return strategies[rndIndex];
        }
    }
}