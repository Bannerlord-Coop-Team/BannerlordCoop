using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TaleWorlds.CampaignSystem;

namespace E2E.Tests.Util.ObjectBuilders;
internal class HeroBuilder : IObjectBuilder
{
    public object Build()
    {
        var characterObject = GameObjectCreator.CreateInitializedObject<CharacterObject>();
        var clan = GameObjectCreator.CreateInitializedObject<Clan>();

        var hero = HeroCreator.CreateSpecialHero(characterObject);

        hero.Clan = clan;

        return hero;
    }
}
