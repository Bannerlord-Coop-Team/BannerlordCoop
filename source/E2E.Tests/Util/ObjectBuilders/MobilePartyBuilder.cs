using Common.Util;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Party.PartyComponents;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Library;

namespace E2E.Tests.Util.ObjectBuilders;
internal class MobilePartyBuilder : IObjectBuilder
{
    public object Build()
    {
        var spawnSettlement = GameObjectCreator.CreateInitializedObject<Settlement>();
        var leaderHero = GameObjectCreator.CreateInitializedObject<Hero>();
        var clan = GameObjectCreator.CreateInitializedObject<Clan>();
        
        leaderHero.Clan = clan;
        clan.SetLeader(leaderHero);

        var party = LordPartyComponent.CreateLordParty("TestParty", leaderHero, Vec2.Zero, 0, spawnSettlement, leaderHero);

        party.PartyComponent.MobileParty = party;
        party.LordPartyComponent.SetMobilePartyInternal(party);

        party.Initialize();

        party.ActualClan = clan;

        return party;
    }

    public MobileParty BuildWithHero(Hero hero)
    {
        var componentBuilder = new LordPartyComponentBuilder();

        var clan = GameObjectCreator.CreateInitializedObject<Clan>();

        var partyComponent = componentBuilder.BuildWithHero(hero);

        return MobileParty.CreateParty("This should not set", partyComponent, (party) =>
        {
            using (new AllowedThread())
            {
                party.ActualClan = clan;
            }

            partyComponent.InitializeLordPartyProperties(party, Vec2.Zero, 0, null); 
        });
    }
}
