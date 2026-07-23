using TaleWorlds.CampaignSystem.Settlements;

namespace E2E.Tests.Util.ObjectBuilders;
internal class AlleyBuilder : IObjectBuilder
{
    public object Build()
    {
        var settlement = GameObjectCreator.CreateInitializedObject<Settlement>();
        return new Alley(settlement, "tag", new TaleWorlds.Localization.TextObject("TestAlley"));
    }
}
