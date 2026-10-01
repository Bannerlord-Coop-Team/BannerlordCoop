#if DEBUG
using E2E.Tests.Util;
using GameInterface.Services.Villages.Commands;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using Missions.Battles;
using Xunit.Abstractions;

namespace E2E.Tests.Services.Missions;

/// <summary>Checks that health fixture troops are bounded and original roster values are restored.</summary>
public class HealthFixtureRosterTests : MissionTestEnvironment
{
    public HealthFixtureRosterTests(ITestOutputHelper output) : base(output)
    {
        XpCapModels.Install(Server);
    }

    [Fact]
    public void ProvisionAndRestore_PreservesOriginalTroopCountsWoundsAndExperience()
    {
        var (mapEventId, _) = SetupCoopBattle("attacker", "defender");
        Server.Call(() =>
        {
            var party = Server.GetRegisteredObject<MapEvent>(mapEventId).DefenderSide.Parties[0].Party.MobileParty;
            var oldTroop = GameObjectCreator.CreateInitializedObject<CharacterObject>();
            var newTroop = GameObjectCreator.CreateInitializedObject<CharacterObject>();
            oldTroop.Level = 21;
            newTroop.Level = 26;
            oldTroop.UpgradeTargets = new[] { newTroop };
            newTroop.UpgradeTargets = Array.Empty<CharacterObject>();
            party.MemberRoster.Clear();
            party.MemberRoster.AddToCounts(oldTroop, 7, woundedCount: 2, xpChange: 30);
            var original = party.MemberRoster.GetTroopRoster().ToArray();
            Assert.Equal(30, original[0].Xp);
            MapEventDebugCommands.ProvisionHealthFixtureRoster(party.MemberRoster, newTroop);
            Assert.Equal(1200, party.MemberRoster.GetTroopCount(newTroop));
            Assert.True(party.MemberRoster.TotalHealthyCount > BattleSizeProvider.MaximumBattleSize);
            party.MemberRoster.AddToCounts(newTroop, -1);
            MapEventDebugCommands.RestoreHealthFixtureRosters(
                new Dictionary<MobileParty, TroopRosterElement[]> { [party] = original },
                new Dictionary<Hero, int>());
            Assert.Equal(0, party.MemberRoster.GetTroopCount(newTroop));
            var restored = Assert.Single(party.MemberRoster.GetTroopRoster().Where(element => element.Number > 0));
            Assert.Same(oldTroop, restored.Character);
            Assert.Equal(original[0].Number, restored.Number);
            Assert.Equal(original[0].WoundedNumber, restored.WoundedNumber);
            Assert.Equal(original[0].Xp, restored.Xp);
        });
    }
}
#endif
