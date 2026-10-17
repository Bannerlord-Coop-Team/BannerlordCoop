using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.Siege;
using GameInterface.Services.MobileParties.Extensions;
using Common.Util;
using E2E.Tests.Environment;
using GameInterface.Services.MobileParties;
using GameInterface.Services.Players;
using GameInterface.Services.Players.Data;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;
using Xunit.Abstractions;

namespace E2E.Tests.Services.MobileParties;

public class EmptyLordPartyResetterTests : IDisposable
{
    private readonly E2ETestEnvironment environment;

    public EmptyLordPartyResetterTests(ITestOutputHelper output)
    {
        environment = new E2ETestEnvironment(output);
    }

    public void Dispose() => environment.Dispose();

    [Fact]
    public void Reset_LordPartyWithNoHealthyMen_DestroysPartyAndMakesLordFugitive()
    {
        var (heroId, partyId) = CreateLordParty(healthyTroops: 0);

        environment.Server.Call(() =>
        {
            var hero = environment.Server.GetRegisteredObject<Hero>(heroId);

            Assert.Equal(1, environment.Server.Resolve<IEmptyLordPartyResetter>().ResetEmptyLordParties());

            Assert.Equal(Hero.CharacterStates.Fugitive, hero.HeroState);
            Assert.True(hero.IsAlive);
            Assert.Null(hero.PartyBelongedTo);
        });

        Assert.False(environment.Server.ObjectManager.TryGetObject<MobileParty>(partyId, out _));
        foreach (var client in environment.Clients)
            Assert.False(client.ObjectManager.TryGetObject<MobileParty>(partyId, out _));
    }

    [Fact]
    public void Reset_LordPartyWithHealthyMen_LeavesItAlone()
    {
        var (heroId, partyId) = CreateLordParty(healthyTroops: 5);

        environment.Server.Call(() =>
        {
            var hero = environment.Server.GetRegisteredObject<Hero>(heroId);
            var party = environment.Server.GetRegisteredObject<MobileParty>(partyId);

            Assert.Equal(0, environment.Server.Resolve<IEmptyLordPartyResetter>().ResetEmptyLordParties());

            Assert.Equal(Hero.CharacterStates.Active, hero.HeroState);
            Assert.Same(party, hero.PartyBelongedTo);
            Assert.True(party.IsActive);
        });
    }

    [Fact]
    public void Reset_PlayerPartyWithNoHealthyMen_LeavesItAlone()
    {
        var (heroId, partyId) = CreateLordParty(healthyTroops: 0);

        environment.Server.Call(() =>
        {
            environment.Server.Resolve<IPlayerManager>()
                .AddPlayer(new Player("empty-player", heroId, partyId, "MyClanId", "MyCharacterObjectId"));
            var hero = environment.Server.GetRegisteredObject<Hero>(heroId);

            Assert.Equal(0, environment.Server.Resolve<IEmptyLordPartyResetter>().ResetEmptyLordParties());

            Assert.Equal(Hero.CharacterStates.Active, hero.HeroState);
            Assert.True(environment.Server.GetRegisteredObject<MobileParty>(partyId).IsActive);
        });
    }
    [Fact]
    public void Reset_AllTroopsWounded_LeavesItAlone()
    {
        // wounded troops heal, so the party is not stuck (#3916 review)
        var (heroId, partyId) = CreateLordParty(healthyTroops: 0);
        var troopId = environment.CreateRegisteredObject<CharacterObject>();

        environment.Server.Call(() =>
        {
            var party = environment.Server.GetRegisteredObject<MobileParty>(partyId);
            var troop = environment.Server.GetRegisteredObject<CharacterObject>(troopId);
            party.MemberRoster.AddToCounts(troop, 6, woundedCount: 6);
            Assert.Equal(0, party.MemberRoster.TotalHealthyCount);

            Assert.Equal(0, environment.Server.Resolve<IEmptyLordPartyResetter>().ResetEmptyLordParties());

            Assert.Equal(Hero.CharacterStates.Active, environment.Server.GetRegisteredObject<Hero>(heroId).HeroState);
            Assert.True(party.IsActive);
        });
    }

    [Fact]
    public void IsStrandedLordParty_InSettlementOrBesieging_IsFalse()
    {
        var (_, partyId) = CreateLordParty(healthyTroops: 0);
        var settlementId = environment.CreateRegisteredObject<Settlement>();

        environment.Server.Call(() =>
        {
            var party = environment.Server.GetRegisteredObject<MobileParty>(partyId);
            Assert.True(EmptyLordPartyResetter.IsStrandedLordParty(party));

            party.SetCurrentSettlementDirectly(environment.Server.GetRegisteredObject<Settlement>(settlementId));
            Assert.False(EmptyLordPartyResetter.IsStrandedLordParty(party));

            party.SetCurrentSettlementDirectly(null);
            party._besiegerCamp = ObjectHelper.SkipConstructor<BesiegerCamp>();
            Assert.False(EmptyLordPartyResetter.IsStrandedLordParty(party));
        });
    }
    [Fact]
    public void IsStrandedLordParty_PrisonerOrDeadLord_IsFalse()
    {
        var (heroId, partyId) = CreateLordParty(healthyTroops: 0);

        environment.Server.Call(() =>
        {
            var hero = environment.Server.GetRegisteredObject<Hero>(heroId);
            var party = environment.Server.GetRegisteredObject<MobileParty>(partyId);
            Assert.True(EmptyLordPartyResetter.IsStrandedLordParty(party));

            hero._heroState = Hero.CharacterStates.Prisoner;
            Assert.False(EmptyLordPartyResetter.IsStrandedLordParty(party));

            hero._heroState = Hero.CharacterStates.Dead;
            Assert.False(EmptyLordPartyResetter.IsStrandedLordParty(party));
        });
    }

    private (string heroId, string partyId) CreateLordParty(int healthyTroops)
    {
        // registered test parties are lord parties led by their own hero
        var partyId = environment.CreateRegisteredObject<MobileParty>();
        var troopId = environment.CreateRegisteredObject<CharacterObject>();
        string heroId = null;
        environment.Server.Call(() =>
        {
            var party = environment.Server.GetRegisteredObject<MobileParty>(partyId);
            var troop = environment.Server.GetRegisteredObject<CharacterObject>(troopId);
            var hero = party.LeaderHero;
            Assert.True(environment.Server.ObjectManager.TryGetId(hero, out heroId));
            hero._heroState = Hero.CharacterStates.Active;
            hero.PartyBelongedTo = party;
            // test parties spawn inside a settlement; a stranded party is out on the map
            party.SetCurrentSettlementDirectly(null);
            Assert.Null(party.SiegeEvent);
            Assert.Null(party.BesiegerCamp);

            // test parties come with soldiers; a stranded party has none left, only its wounded lord
            hero.HitPoints = 1;
            var roster = party.MemberRoster;
            for (int i = roster.Count - 1; i >= 0; i--)
            {
                var element = roster.GetElementCopyAtIndex(i);
                if (element.Character.IsHero)
                    roster.SetElementWoundedNumber(i, element.Number);
                else
                    roster.AddToCountsAtIndex(i, -element.Number, -element.WoundedNumber);
            }
            roster.RemoveZeroCounts();
            roster.InitializeCachedData();
            if (healthyTroops > 0)
                roster.AddToCounts(troop, healthyTroops);

            Assert.True(party.IsActive);
            Assert.True(party.IsLordParty);
            Assert.Equal(healthyTroops, roster.TotalHealthyCount);
        });
        return (heroId, partyId);
    }}
