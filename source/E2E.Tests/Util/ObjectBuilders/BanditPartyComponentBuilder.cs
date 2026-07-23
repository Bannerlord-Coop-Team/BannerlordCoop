using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Party.PartyComponents;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Library;

namespace E2E.Tests.Util.ObjectBuilders;
internal class BanditPartyComponentBuilder : IObjectBuilder
{
    public object Build()
    {
        var hideout = GameObjectCreator.CreateInitializedObject<Hideout>();
        var clan = GameObjectCreator.CreateInitializedObject<Clan>();
        var template = GameObjectCreator.CreateInitializedObject<PartyTemplateObject>();

        return new BanditPartyComponent(hideout, false, new BanditPartyComponent.InitializationArgs(clan, template, new CampaignVec2(new Vec2(2, 2), true)));
    }
}
