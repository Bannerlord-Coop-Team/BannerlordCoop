using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Party.PartyComponents;
using TaleWorlds.Library;

namespace E2E.Tests.Util.ObjectBuilders;
internal class MobilePartyBuilder : IObjectBuilder
{
    public object Build()
    {
        var partyComponent = GameObjectCreator.CreateInitializedObject<LordPartyComponent>();

        return MobileParty.CreateParty("This should not set", partyComponent, (party) =>
        {
            AccessTools.Method(typeof(LordPartyComponent), "InitializeLordPartyProperties")
                .Invoke(partyComponent, new object[] { party, Vec2.Zero, 0, null });
        });
    }

    public MobileParty BuildWithHero(Hero hero)
    {
        var componentBuilder = new LordPartyComponentBuilder();

        var partyComponent = componentBuilder.BuildWithHero(hero);

        return MobileParty.CreateParty("This should not set", partyComponent, (party) =>
        {
            AccessTools.Method(typeof(LordPartyComponent), "InitializeLordPartyProperties")
                .Invoke(partyComponent, new object[] { party, Vec2.Zero, 0, null });
        });
    }
}
