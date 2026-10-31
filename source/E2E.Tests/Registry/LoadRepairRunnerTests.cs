using E2E.Tests.Environment;
using E2E.Tests.Util;
using GameInterface.Policies;
using GameInterface.Registry;
using GameInterface.Services.Heroes.HeirSelection.Interfaces;
using GameInterface.Services.MobileParties.Data;
using GameInterface.Services.MobileParties.Extensions;
using HarmonyLib;
using System.Reflection;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using Xunit.Abstractions;

namespace E2E.Tests.Registry;

/// <summary>
/// The server runs the load repairs at CampaignReady, after the first registration and before
/// ServerRunningState, while its sync policy still allows originals. Lifetime patches publish no
/// InstanceCreated or InstanceDestroyed in that window, so these tests run the repairs the same way.
/// </summary>
public class LoadRepairRunnerTests : IDisposable
{
    private readonly E2ETestEnvironment environment;

    // test heroes have no encyclopedia data, which the obituary and retirement log text need,
    // and a disbanded army refreshes visibility against the main party, which the E2E server has none of
    private static readonly MethodBase[] DisabledMethods =
    {
        AccessTools.Method(typeof(KillCharacterAction), "CreateObituary"),
        AccessTools.Method(typeof(ApplyHeirSelectionActionInterface), "RecordRetirement"),
        AccessTools.Method(typeof(PartyBaseVisibilityExtensions), nameof(PartyBaseVisibilityExtensions.UpdateVisibilityAndInspected)),
    };

    public LoadRepairRunnerTests(ITestOutputHelper output)
    {
        environment = new E2ETestEnvironment(output);
    }

    public void Dispose() => environment.Dispose();

    [Fact]
    public void RunLoadRepairs_SuccessorWithoutParty_GetsARegisteredPartyForTheJoinBaseline()
    {
        var clanId = CreateKingdomClan();
        var heroId = AddMember(clanId, age: 30);
        var settlementId = environment.CreateRegisteredObject<Settlement>();

        environment.Server.Call(() =>
        {
            var clan = environment.Server.GetRegisteredObject<Clan>(clanId);
            var hero = environment.Server.GetRegisteredObject<Hero>(heroId);
            // a hero waiting in a settlement gets a new party there when appointed
            hero.StayingInSettlement = environment.Server.GetRegisteredObject<Settlement>(settlementId);
            Assert.Null(hero.PartyBelongedTo);
            Assert.Null(clan.Leader);

            RunLoadRepairsInBootWindow();

            Assert.Same(hero, clan.Leader);
            var party = hero.PartyBelongedTo;
            Assert.NotNull(party);
            Assert.Contains(party, Campaign.Current.CampaignObjectManager.MobileParties);
            Assert.True(environment.Server.ObjectManager.TryGetId(party, out _));

            var liveParties = new HashSet<MobileParty>(
                Campaign.Current.CampaignObjectManager.MobileParties.Where(mobileParty => mobileParty.IsActive));
            var liveSettlements = new HashSet<Settlement>(Settlement.All);
            var created = environment.Server.Resolve<IMobilePartyBehaviorSnapshot>().TryCreateJoinState(
                party, liveParties, liveSettlements, out _, out var failure);
            Assert.True(created, failure);
        }, DisabledMethods);
    }

    [Fact]
    public void RunLoadRepairs_EmptyArmyLeaderParty_IsUnregisteredWithItsArmy()
    {
        var (heroId, partyId) = CreateStrandedLordParty();
        string armyId = null;
        string aiId = null;
        environment.Server.Call(() =>
        {
            var party = environment.Server.GetRegisteredObject<MobileParty>(partyId);
            Assert.True(environment.Server.ObjectManager.TryGetId(party.Ai, out aiId));
            // a saved army belongs to a campaign kingdom, the list ArmyRegistry registers armies from
            var kingdom = GameObjectCreator.CreateInitializedObject<Kingdom>();
            Campaign.Current.CampaignObjectManager.AddKingdom(kingdom);
            var army = new Army(kingdom, party, Army.ArmyTypes.Patrolling);
            Assert.True(environment.Server.ObjectManager.TryGetId(army, out armyId));
        });

        environment.Server.Call(() =>
        {
            var hero = environment.Server.GetRegisteredObject<Hero>(heroId);
            var party = environment.Server.GetRegisteredObject<MobileParty>(partyId);
            var army = party.Army;
            var kingdom = army.Kingdom;

            RunLoadRepairsInBootWindow();

            Assert.Equal(Hero.CharacterStates.Fugitive, hero.HeroState);
            Assert.DoesNotContain(party, Campaign.Current.CampaignObjectManager.MobileParties);
            Assert.DoesNotContain(army, kingdom.Armies);
        }, DisabledMethods);

        Assert.False(environment.Server.ObjectManager.TryGetObject<MobileParty>(partyId, out _));
        Assert.False(environment.Server.ObjectManager.TryGetObject<Army>(armyId, out _));
        // the live RemoveParty postfix unregisters the party's Ai, and it publishes nothing in this window
        Assert.False(environment.Server.ObjectManager.TryGetObject<MobilePartyAi>(aiId, out _));
    }

    [Fact]
    public void RunLoadRepairs_ClanClosedWithAWandererCompanion_RegistersTheCompanionsResetEquipment()
    {
        // #4166: a kingdom clan nobody can lead is closed, and firing its wanderer companion resets the companion's
        // equipment to fresh template clones that need the owner-derived ids the old equipment still holds
        var clanId = CreateKingdomClan();
        var companionId = environment.CreateRegisteredObject<Hero>();
        environment.Server.Call(() =>
        {
            var clan = environment.Server.GetRegisteredObject<Clan>(clanId);
            var companion = environment.Server.GetRegisteredObject<Hero>(companionId);
            companion._heroState = Hero.CharacterStates.Active;
            companion.SetBirthDay(CampaignTime.YearsFromNow(-30));
            companion.SetNewOccupation(Occupation.Wanderer);
            companion.CompanionOf = clan;
            Assert.Contains(companion, clan.Heroes);

            // a loaded save registers hero equipment under owner-derived ids, not the generated ids a live
            // constructor gives it, so register it the way the first registration does
            foreach (var equipment in HeroEquipments(companion))
                environment.Server.ObjectManager.Remove(equipment);
            environment.Server.Resolve<IRegistryManager>().RegisterNewGameObjects();
            Assert.True(environment.Server.ObjectManager.TryGetId(companion.BattleEquipment, out var savedId));
            Assert.Equal($"Equipment_{companion.StringId}_BattleEquipment", savedId);
        });

        environment.Server.Call(() =>
        {
            var clan = environment.Server.GetRegisteredObject<Clan>(clanId);
            var companion = environment.Server.GetRegisteredObject<Hero>(companionId);
            var before = HeroEquipments(companion);
            Assert.NotEmpty(before);

            RunLoadRepairsInBootWindow();

            Assert.True(clan.IsEliminated);
            Assert.True(companion.IsAlive);
            Assert.Null(companion.CompanionOf);
            var after = HeroEquipments(companion);
            Assert.Equal(before.Length, after.Length);
            foreach (var equipment in before)
            {
                Assert.DoesNotContain(equipment, after);
                Assert.False(environment.Server.ObjectManager.Contains(equipment));
            }

            foreach (var equipment in after)
            {
                Assert.True(environment.Server.ObjectManager.TryGetId(equipment, out var id));
                Assert.True(environment.Server.ObjectManager.TryGetObject<Equipment>(id, out var registered));
                Assert.Same(equipment, registered);
            }
        }, DisabledMethods);
    }

    private static Equipment[] HeroEquipments(Hero hero) =>
        new[] { hero.BattleEquipment, hero.CivilianEquipment, hero.StealthEquipment }
            .Where(equipment => equipment != null)
            .ToArray();

    private void RunLoadRepairsInBootWindow()
    {
        var policy = (TestPolicy)environment.Server.Resolve<ISyncPolicy>();
        var previous = policy.AllowOriginals;
        policy.AllowOriginals = true;
        try
        {
            environment.Server.Resolve<ILoadRepairRunner>().RunLoadRepairs();
        }
        finally
        {
            policy.AllowOriginals = previous;
        }
    }

    private string CreateKingdomClan()
    {
        string clanId = null;
        environment.Server.Call(() =>
        {
            var kingdom = GameObjectCreator.CreateInitializedObject<Kingdom>();
            var clan = Clan.CreateClan("LoadRepairClan");
            clan._defaultPartyTemplate = GameObjectCreator.CreateInitializedObject<PartyTemplateObject>();
            clan.Culture = GameObjectCreator.CreateInitializedObject<CultureObject>();
            clan._kingdom = kingdom;
            kingdom._clans.Add(clan);
            Assert.True(environment.Server.ObjectManager.TryGetId(clan, out clanId));
        });
        return clanId;
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

    // a registered lord party out on the map with only its wounded lord left
    private (string heroId, string partyId) CreateStrandedLordParty()
    {
        var partyId = environment.CreateRegisteredObject<MobileParty>();
        string heroId = null;
        environment.Server.Call(() =>
        {
            var party = environment.Server.GetRegisteredObject<MobileParty>(partyId);
            var hero = party.LeaderHero;
            Assert.True(environment.Server.ObjectManager.TryGetId(hero, out heroId));
            hero._heroState = Hero.CharacterStates.Active;
            hero.PartyBelongedTo = party;
            party.SetCurrentSettlementDirectly(null);

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

            Assert.True(party.IsActive);
            Assert.True(party.IsLordParty);
            Assert.Equal(0, roster.TotalHealthyCount);
        });
        return (heroId, partyId);
    }
}
