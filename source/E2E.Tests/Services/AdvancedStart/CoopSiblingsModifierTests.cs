using E2E.Tests.Environment;
using E2E.Tests.Environment.Instance;
using E2E.Tests.Util;
using GameInterface.Services.AdvancedStart;
using HarmonyLib;
using Helpers;
using System.Reflection;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;
using TaleWorlds.Localization;
using Xunit.Abstractions;

namespace E2E.Tests.Services.AdvancedStart;

public class CoopSiblingsModifierTests : IDisposable
{
    private E2ETestEnvironment TestEnvironment { get; }
    private readonly Harmony clock = new($"e2e.coop-siblings.{Guid.NewGuid():N}");

    // The test bootstrap pins CampaignTime.Now, but Hero.Age reads the map time tracker, so ages need both on one clock.
    public CoopSiblingsModifierTests(ITestOutputHelper output)
    {
        TestEnvironment = new E2ETestEnvironment(output);
        clock.Patch(AccessTools.PropertyGetter(typeof(CampaignTime), nameof(CampaignTime.Now)),
            postfix: new HarmonyMethod(typeof(CoopSiblingsModifierTests), nameof(ProvideCampaignTime)));
    }

    public void Dispose()
    {
        clock.UnpatchAll(clock.Id);
        TestEnvironment.Dispose();
    }

    private static void ProvideCampaignTime(ref CampaignTime __result) => __result = Campaign.Current.MapTimeTracker.Now;

    [Fact]
    public void GiveSiblings_GivesDeadParentsAndAdultSiblingsInTheParty_OnEveryMachine()
    {
        var server = TestEnvironment.Server;
        string partyId = TestEnvironment.CreateRegisteredObject<MobileParty>();
        string? heroId = null;

        server.Call(() =>
        {
            Assert.True(server.ObjectManager.TryGetObject<MobileParty>(partyId, out var party));
            var hero = party.LeaderHero;
            Assert.True(server.ObjectManager.TryGetId(hero, out heroId));
            hero.SetBirthDay(HeroHelper.GetRandomBirthDayForAge(HeroAge));

            var maleTemplate = CreateTemplate(hero.Culture, isFemale: false);
            var femaleTemplate = CreateTemplate(hero.Culture, isFemale: true);

            CoopSiblingsModifier.GiveSiblings(hero, 2,
                new List<CharacterObject> { maleTemplate }, new List<CharacterObject> { femaleTemplate });
        }, WithoutObituary);
        server.PumpGameThread();
        TestEnvironment.FlushCoalescer();
        foreach (var client in TestEnvironment.Clients) client.PumpGameThread();

        server.Call(() => AssertSiblings(server, heroId!, partyId, assertHeroState: true));
        // The test bootstrap turns ChangeState into a field store, so hero states only show on the server.
        foreach (var client in TestEnvironment.Clients)
            client.Call(() => AssertSiblings(client, heroId!, partyId, assertHeroState: false));
    }

    private static void AssertSiblings(EnvironmentInstance instance, string heroId, string partyId, bool assertHeroState)
    {
        Assert.True(instance.ObjectManager.TryGetObject<Hero>(heroId, out var hero));
        Assert.True(instance.ObjectManager.TryGetObject<MobileParty>(partyId, out var party));

        Assert.NotNull(hero.Father);
        Assert.NotNull(hero.Mother);
        Assert.DoesNotContain(WandererTitle, hero.Mother.Name.ToString());
        // The mother comes from a wanderer template, which vanilla would give the template's own age.
        foreach (var parent in new[] { hero.Father, hero.Mother })
        {
            Assert.InRange(parent.Age, HeroAge + CoopSiblingsModifier.MinParentAgeGap,
                HeroAge + CoopSiblingsModifier.MaxParentAgeGap + 1);
        }
        if (assertHeroState)
        {
            Assert.False(hero.Father.IsAlive);
            Assert.False(hero.Mother.IsAlive);
        }

        var siblings = party.MemberRoster.GetTroopRoster()
            .Select(element => element.Character.HeroObject)
            .Where(member => member != null && member != hero)
            .ToList();
        Assert.Equal(2, siblings.Count);

        foreach (var sibling in siblings)
        {
            Assert.Same(hero.Father, sibling.Father);
            Assert.Same(hero.Mother, sibling.Mother);
            Assert.Same(hero.Clan, sibling.Clan);
            Assert.DoesNotContain(WandererTitle, sibling.Name.ToString());
            if (assertHeroState)
                Assert.Equal(Hero.CharacterStates.Active, sibling.HeroState);
            Assert.InRange(sibling.Age, HeroAge - CoopSiblingsModifier.MaxSiblingAgeGap,
                HeroAge + CoopSiblingsModifier.MaxSiblingAgeGap + 1);
            Assert.Same(party, sibling.PartyBelongedTo);
        }
    }

    private const string WandererTitle = "the Smith";
    // Old enough that a sibling the full gap younger is still an adult.
    private const int HeroAge = 40;

    private static CharacterObject CreateTemplate(CultureObject culture, bool isFemale)
    {
        var template = GameObjectCreator.CreateInitializedObject<CharacterObject>();
        template.Culture = culture;
        template.IsFemale = isFemale;
        template.IsTemplate = true;
        // Like the real picks, men from lord templates and women from wanderer templates titled after their trade.
        template._occupation = isFemale ? Occupation.Wanderer : Occupation.Lord;
        if (isFemale)
        {
            template._basicName = new TextObject("{FIRSTNAME} " + WandererTitle);
            // A young adult like the real wanderer templates, whose age vanilla would use instead of the one asked for.
            template._age = 22;
        }
        return template;
    }

    // The obituary needs the encyclopedia, which the E2E campaign does not load.
    private static IEnumerable<MethodBase> WithoutObituary => new[]
    {
        AccessTools.Method(typeof(KillCharacterAction), nameof(KillCharacterAction.CreateObituary)),
    };
}
