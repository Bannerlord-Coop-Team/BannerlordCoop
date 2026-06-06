using Common.Util;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Localization;

namespace E2E.Tests.Util.ObjectBuilders;
internal class ClanBuilder : IObjectBuilder
{
    public object Build()
    {
        var clan = new Clan();
        var defaultTemplate = GameObjectCreator.CreateInitializedObject<PartyTemplateObject>();
        clan._defaultPartyTemplate = defaultTemplate;

        // To make this unique
        clan.Name = new TextObject(Guid.NewGuid().ToString());

        return clan;
    }
}
