using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using E2E.Tests.Environment;
using E2E.Tests.Util;
using GameInterface.Services.Clans;
using GameInterface.Services.Heroes.HeirSelection.Interfaces;
using HarmonyLib;
using System.Reflection;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using Xunit.Abstractions;

namespace E2E.Tests.Services.Clans;

public class LeaderlessClanResolverTests : IDisposable
{
    private readonly E2ETestEnvironment environment;

    // test heroes have no encyclopedia data, which the obituary and retirement log text need
    private static readonly MethodBase[] TextMethods =
    {
        AccessTools.Method(typeof(KillCharacterAction), "CreateObituary"),
        AccessTools.Method(typeof(ApplyHeirSelectionActionInterface), "RecordRetirement"),
    };

    public LeaderlessClanResolverTests(ITestOutputHelper output)
    {
        environment = new E2ETestEnvironment(output);
    }

    public void Dispose() => environment.Dispose();

    [Fact]
    public void Resolve_KingdomClanWithNobodyToLead_DestroysItOnEveryPeer()
    {
        var clanId = CreateKingdomClan(out _);

        environment.Server.Call(() =>
        {
            var clan = environment.Server.GetRegisteredObject<Clan>(clanId);
            Assert.Null(clan.Leader);
            environment.Server.Resolve<ILeaderlessClanResolver>().Resolve(clan);
        });

        Assert.False(environment.Server.ObjectManager.TryGetObject<Clan>(clanId, out _));
        foreach (var client in environment.Clients)
            Assert.False(client.ObjectManager.TryGetObject<Clan>(clanId, out _));
    }

    [Fact]
    public void Resolve_NullLeaderWithAnAdult_AppointsThemWithTheLeaderChangeSteps()
    {
        // #3928 review: SetLeader alone skipped the governor, party and leader-changed event
        var clanId = CreateKingdomClan(out _);
        var heroId = AddPartyLeader(clanId, age: 30);
        var townId = environment.CreateRegisteredObject<Town>();


        environment.Server.Call(() =>
        {
            var clan = environment.Server.GetRegisteredObject<Clan>(clanId);
            var hero = environment.Server.GetRegisteredObject<Hero>(heroId);
            var town = environment.Server.GetRegisteredObject<Town>(townId);
            town.Governor = hero;
            Assert.Same(town, hero.GovernorOf);

            Hero changedTo = null;
            CampaignEvents.OnClanLeaderChangedEvent.AddNonSerializedListener(this, (oldLeader, newLeader) => changedTo = newLeader);
            try
            {
                environment.Server.Resolve<ILeaderlessClanResolver>().Resolve(clan);
            }
            finally
            {
                CampaignEvents.OnClanLeaderChangedEvent.ClearListeners(this);
            }

            Assert.Same(hero, clan.Leader);
            Assert.Null(hero.GovernorOf);
            Assert.Same(hero, hero.PartyBelongedTo.LeaderHero);
            Assert.Same(hero, changedTo);
        }, TextMethods);
    }

    [Fact]
    public void Resolve_DisabledLeader_HandsOverWithTheGameAction()
    {
        var clanId = CreateKingdomClan(out _);
        var formerId = AddMember(clanId, age: 60);
        var heroId = AddPartyLeader(clanId, age: 30);

        environment.Server.Call(() =>
        {
            var clan = environment.Server.GetRegisteredObject<Clan>(clanId);
            var former = environment.Server.GetRegisteredObject<Hero>(formerId);
            clan.SetLeader(former);
            former._heroState = Hero.CharacterStates.Disabled;

            environment.Server.Resolve<ILeaderlessClanResolver>().Resolve(clan);

            Assert.Same(environment.Server.GetRegisteredObject<Hero>(heroId), clan.Leader);
        }, TextMethods);
    }

    [Fact]
    public void Resolve_OnlyAChild_MovesTheChildToTheParentsClanBeforeClosing()
    {
        // #3928 review: destroying the clan used to kill the child
        var clanId = CreateKingdomClan(out _);
        var childId = AddMember(clanId, age: 8);
        var fatherClanId = CreateKingdomClan(out _);
        var fatherId = AddMember(fatherClanId, age: 40);

        environment.Server.Call(() =>
        {
            var child = environment.Server.GetRegisteredObject<Hero>(childId);
            var father = environment.Server.GetRegisteredObject<Hero>(fatherId);
            child.Father = father;

            environment.Server.Resolve<ILeaderlessClanResolver>().Resolve(environment.Server.GetRegisteredObject<Clan>(clanId));

            Assert.True(child.IsAlive);
            Assert.Same(environment.Server.GetRegisteredObject<Clan>(fatherClanId), child.Clan);
        }, TextMethods);

        Assert.False(environment.Server.ObjectManager.TryGetObject<Clan>(clanId, out _));
    }
    [Fact]
    public void RepairLoadedClans_LeavesClansWithALeaderOrNoKingdomAlone()
    {
        var leaderlessId = CreateKingdomClan(out _);
        var ledId = CreateKingdomClan(out _);
        var leaderId = AddMember(ledId, age: 40);
        string freeClanId = null;
        environment.Server.Call(() =>
        {
            environment.Server.GetRegisteredObject<Clan>(ledId).SetLeader(environment.Server.GetRegisteredObject<Hero>(leaderId));
            Assert.True(environment.Server.ObjectManager.TryGetId(Clan.CreateClan("FreeClan"), out freeClanId));

            Assert.Equal(1, environment.Server.Resolve<ILeaderlessClanResolver>().RepairLoadedClans());
        });

        Assert.False(environment.Server.ObjectManager.TryGetObject<Clan>(leaderlessId, out _));
        Assert.True(environment.Server.ObjectManager.TryGetObject<Clan>(ledId, out _));
        Assert.True(environment.Server.ObjectManager.TryGetObject<Clan>(freeClanId, out _));
    }

    [Fact]
    public void RetirementWithoutHeir_LastLord_DoesNotLeaveALeaderlessKingdomClan()
    {
        // #4166: the retiring leader had nobody to succeed them and the clan stayed in its kingdom without a leader
        var clanId = CreateKingdomClan(out _);
        var heroId = AddMember(clanId, age: 40);
        environment.Server.Call(() =>
        {
            var clan = environment.Server.GetRegisteredObject<Clan>(clanId);
            var hero = environment.Server.GetRegisteredObject<Hero>(heroId);
            clan.SetLeader(hero);

            environment.Server.Resolve<IApplyHeirSelectionActionInterface>().ApplyByRetirementWithoutHeir(hero);
        }, TextMethods);

        if (environment.Server.ObjectManager.TryGetObject<Clan>(clanId, out var remaining))
            Assert.False(LeaderlessClanResolver.NeedsLeader(remaining));

        // #3928 review: closing the clan must not turn the retirement into a death
        environment.Server.Call(() =>
        {
            var hero = environment.Server.GetRegisteredObject<Hero>(heroId);
            Assert.Equal(Hero.CharacterStates.Disabled, hero.HeroState);
            Assert.Equal(KillCharacterAction.KillCharacterActionDetail.None, hero.DeathMark);
        });
    }

    private string CreateKingdomClan(out string kingdomId)
    {
        string clanId = null;
        string kId = null;
        environment.Server.Call(() =>
        {
            var kingdom = GameObjectCreator.CreateInitializedObject<Kingdom>();
            var clan = Clan.CreateClan("LeaderlessClan");
            clan._defaultPartyTemplate = GameObjectCreator.CreateInitializedObject<PartyTemplateObject>();
            clan._kingdom = kingdom;
            Assert.True(environment.Server.ObjectManager.TryGetId(clan, out clanId));
            Assert.True(environment.Server.ObjectManager.TryGetId(kingdom, out kId));
        });
        kingdomId = kId;
        return clanId;
    }

    // a hero who already leads a party: creating a new one needs the world map, which tests don't have
    private string AddPartyLeader(string clanId, int age)
    {
        var partyId = environment.CreateRegisteredObject<MobileParty>();
        string heroId = null;
        environment.Server.Call(() =>
        {
            var hero = environment.Server.GetRegisteredObject<MobileParty>(partyId).LeaderHero;
            hero._heroState = Hero.CharacterStates.Active;
            hero.SetBirthDay(CampaignTime.YearsFromNow(-age));
            hero.Clan = environment.Server.GetRegisteredObject<Clan>(clanId);
            Assert.True(environment.Server.ObjectManager.TryGetId(hero, out heroId));
        });
        return heroId;
    }
    private string AddMember(string clanId, int age)
    {
        var heroId = environment.CreateRegisteredObject<Hero>();
        environment.Server.Call(() =>
        {
            var hero = environment.Server.GetRegisteredObject<Hero>(heroId);
            hero._heroState = Hero.CharacterStates.Active;
            hero.SetBirthDay(CampaignTime.YearsFromNow(-age));
            hero.Clan = environment.Server.GetRegisteredObject<Clan>(clanId);
        });
        return heroId;
    }
}