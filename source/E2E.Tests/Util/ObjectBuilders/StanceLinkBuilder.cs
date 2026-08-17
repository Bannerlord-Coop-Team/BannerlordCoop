using TaleWorlds.CampaignSystem;

namespace E2E.Tests.Util.ObjectBuilders;

internal class StanceLinkBuilder : IObjectBuilder
{
    public object Build()
    {
        var faction1 = Kingdom.CreateKingdom("test_kingdom_1");
        var faction2 = Kingdom.CreateKingdom("test_kingdom_2");

        return FactionManager.Instance.GetStanceLinkInternal(faction1, faction2);
    }
}
