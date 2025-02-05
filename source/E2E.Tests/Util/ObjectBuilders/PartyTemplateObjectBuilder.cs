using Common.Util;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
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

        var templateCharacter = new CharacterObject();

        partyTemplate.Stacks.Add(new PartyTemplateStack(templateCharacter, 0, 100));

        return partyTemplate;
    }
}
