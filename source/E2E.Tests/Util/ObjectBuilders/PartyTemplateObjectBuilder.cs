using Common.Util;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Library;

namespace E2E.Tests.Util.ObjectBuilders;
internal class PartyTemplateObjectBuilder : IObjectBuilder
{
    public object Build()
    {
        var partyTemplate = ObjectHelper.SkipConstructor<PartyTemplateObject>();

        partyTemplate.Stacks = new MBList<PartyTemplateStack>();
        partyTemplate.ShipHulls = new MBList<ShipTemplateStack>();

        var templateCharacter = new CharacterObject();

        partyTemplate.Stacks.Add(new PartyTemplateStack(templateCharacter, 0, 100));

        return partyTemplate;
    }
}
