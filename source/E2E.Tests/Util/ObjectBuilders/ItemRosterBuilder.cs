using TaleWorlds.CampaignSystem.Roster;

namespace E2E.Tests.Util.ObjectBuilders;
internal class ItemRosterBuilder : IObjectBuilder
{
    public object Build()
    {
        return new ItemRoster();
    }
}
