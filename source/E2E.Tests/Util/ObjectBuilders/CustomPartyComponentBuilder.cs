using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party.PartyComponents;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Library;
using TaleWorlds.Localization;

namespace E2E.Tests.Util.ObjectBuilders;
internal class CustomPartyComponentBuilder : IObjectBuilder
{
    public object Build()
    {
        var settlement = GameObjectCreator.CreateInitializedObject<Settlement>();
        var hero = GameObjectCreator.CreateInitializedObject<Hero>();
        var clan = GameObjectCreator.CreateInitializedObject<Clan>();
        return new CustomPartyComponent(settlement,
            new TextObject(""),
            hero,
            "mount",
            "harness",
            2f,
            false,
            new CustomPartyComponent.InitializationArgs(new CampaignVec2(new Vec2(2, 2), true), 2f, clan)
            );
    }
}
