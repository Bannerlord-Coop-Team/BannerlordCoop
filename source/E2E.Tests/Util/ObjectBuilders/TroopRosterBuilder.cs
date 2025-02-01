using TaleWorlds.CampaignSystem.Roster;

namespace E2E.Tests.Util.ObjectBuilders
{
    internal class TroopRosterBuilder : IObjectBuilder
    {
        public object Build()
        {
            return new TroopRoster();
        }
    }
}
