using Common.Util;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TaleWorlds.CampaignSystem.Party.PartyComponents;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;

namespace E2E.Tests.Util.ObjectBuilders;
internal class LordPartyComponentBuilder : IObjectBuilder
{
    public object Build()
    {
        var partyComponent = ObjectHelper.SkipConstructor<LordPartyComponent>();
        var hero = GameObjectCreator.CreateInitializedObject<Hero>();

        AccessTools.Constructor(typeof(LordPartyComponent), new Type[] { typeof(Hero), typeof(Hero) })
            .Invoke(partyComponent, new object[] { hero, hero });

        return partyComponent;
    }

    public LordPartyComponent BuildWithHero(Hero hero)
    {
        var partyComponent = ObjectHelper.SkipConstructor<LordPartyComponent>();
        AccessTools.Constructor(typeof(LordPartyComponent), new Type[] { typeof(Hero), typeof(Hero) })
            .Invoke(partyComponent, new object[] { hero, hero });

        return partyComponent;
    }
}
