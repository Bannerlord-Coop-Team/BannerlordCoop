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
using TaleWorlds.CampaignSystem.Settlements;

namespace E2E.Tests.Util.ObjectBuilders;
internal class LordPartyComponentBuilder : IObjectBuilder
{
    public object Build()
    {
        var partyComponent = ObjectHelper.SkipConstructor<LordPartyComponent>();
        var hero = GameObjectCreator.CreateInitializedObject<Hero>();
        var settlement = GameObjectCreator.CreateInitializedObject<Settlement>();
        var initArgs = new LordPartyComponent.InitializationArgs(new CampaignVec2(), 5f, settlement);

        AccessTools.Constructor(typeof(LordPartyComponent), new Type[] { typeof(Hero), typeof(Hero), typeof(LordPartyComponent.InitializationArgs) })
            .Invoke(partyComponent, new object[] { hero, hero, initArgs });

        return partyComponent;
    }

    public LordPartyComponent BuildWithHero(Hero hero)
    {
        var settlement = GameObjectCreator.CreateInitializedObject<Settlement>();
        var initArgs = new LordPartyComponent.InitializationArgs(new CampaignVec2(), 5f, settlement);

        var partyComponent = ObjectHelper.SkipConstructor<LordPartyComponent>();
        AccessTools.Constructor(typeof(LordPartyComponent), new Type[] { typeof(Hero), typeof(Hero), typeof(LordPartyComponent.InitializationArgs) })
            .Invoke(partyComponent, new object[] { hero, hero, initArgs });

        return partyComponent;
    }
}
