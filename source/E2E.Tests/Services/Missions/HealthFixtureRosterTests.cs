#if DEBUG
using E2E.Tests.Util;
using E2E.Tests.Services.MapEvents;
using GameInterface.Services.Villages.Commands;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using Missions.Battles;
using Xunit.Abstractions;

namespace E2E.Tests.Services.Missions;

/// <summary>Checks that health fixture troops are bounded and original roster values are restored.</summary>
public class HealthFixtureRosterTests : MapEventTestBase
{
    public HealthFixtureRosterTests(ITestOutputHelper output) : base(output)
    {
        XpCapModels.Install(Server);
    }

    [Fact]
    public void ProvisionAndRestore_PreservesOriginalTroopCountsWoundsAndExperience()
    {
        var (heroId, mobilePartyId) = CreatePlayerHeroParty("health-fixture-owner");
        Server.Call(() =>
        {
            var party = Server.GetRegisteredObject<MobileParty>(mobilePartyId);
            var hero = Server.GetRegisteredObject<Hero>(heroId);
            var oldTroop = GameObjectCreator.CreateInitializedObject<CharacterObject>();
            var upgradeTarget = GameObjectCreator.CreateInitializedObject<CharacterObject>();
            oldTroop.Level = 21;
            upgradeTarget.Level = 26;
            oldTroop.UpgradeTargets = new[] { upgradeTarget };
            upgradeTarget.UpgradeTargets = Array.Empty<CharacterObject>();
            party.MemberRoster.RemoveIf(element => !element.Character.IsHero);
            if (party.MemberRoster.GetTroopCount(hero.CharacterObject) == 0)
                party.MemberRoster.AddToCounts(hero.CharacterObject, 1);
            party.ChangePartyLeader(hero);
            party.SetPartyScout(hero);
            party.SetPartySurgeon(hero);
            party.SetPartyEngineer(hero);
            party.SetPartyQuartermaster(hero);
            hero.HitPoints = 84;
            party.MemberRoster.AddToCounts(oldTroop, 7, woundedCount: 2, xpChange: 30);
            var original = party.MemberRoster.GetTroopRoster().ToArray();
            var originalTroop = Assert.Single(original.Where(element => !element.Character.IsHero));
            Assert.Equal(30, originalTroop.Xp);
            MapEventDebugCommands.ProvisionHealthFixtureRoster(party.MemberRoster, oldTroop);
            Assert.Equal(1200, party.MemberRoster.GetTroopCount(oldTroop));
            Assert.Same(party, hero.PartyBelongedTo);
            Assert.Same(hero, party.LeaderHero);
            Assert.Same(hero, party.Scout);
            hero.HitPoints = 24;
            Assert.True(party.MemberRoster.TotalHealthyCount > BattleSizeProvider.MaximumBattleSize);
            party.MemberRoster.AddToCounts(oldTroop, -1);
            party.MemberRoster.SetElementXp(party.MemberRoster.FindIndexOfTroop(oldTroop), 20);
            MapEventDebugCommands.RestoreHealthFixtureRosters(
                new Dictionary<MobileParty, TroopRosterElement[]> { [party] = original },
                new Dictionary<Hero, int> { [hero] = 84 });
            Assert.Equal(0, party.MemberRoster.GetTroopCount(upgradeTarget));
            Assert.Same(party, hero.PartyBelongedTo);
            Assert.Same(hero, party.LeaderHero);
            Assert.Same(hero, party.Scout);
            Assert.Same(hero, party.Surgeon);
            Assert.Same(hero, party.Engineer);
            Assert.Same(hero, party.Quartermaster);
            Assert.Equal(84, hero.HitPoints);
            Assert.Equal(1, party.MemberRoster.GetTroopCount(hero.CharacterObject));
            var restored = Assert.Single(party.MemberRoster.GetTroopRoster().Where(element => !element.Character.IsHero && element.Number > 0));
            Assert.Same(oldTroop, restored.Character);
            Assert.Equal(originalTroop.Number, restored.Number);
            Assert.Equal(originalTroop.WoundedNumber, restored.WoundedNumber);
            Assert.Equal(originalTroop.Xp, restored.Xp);
        });
    }
}
#endif
