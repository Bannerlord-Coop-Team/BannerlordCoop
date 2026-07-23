using TaleWorlds.CampaignSystem.Siege;

namespace E2E.Tests.Util.ObjectBuilders
{
    internal class SiegeStrategyBuilder : IObjectBuilder
    {
        static int InstanceCounter = 0;

        public object Build()
        {
            return new SiegeStrategy($"{nameof(SiegeStrategy)}_{Interlocked.Increment(ref InstanceCounter)}");
        }
    }
}