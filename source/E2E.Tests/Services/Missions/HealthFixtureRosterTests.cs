#if DEBUG
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
    public HealthFixtureRosterTests(ITestOutputHelper output) : base(output) { }

    [Fact]
    public void ProvisionAndRestore_PreservesOriginalTroopCountsWoundsAndExperience()
    {
        var (mapEventId, _) = SetupCoopBattle("attacker", "defender");
        Server.Call(() =>
        {
            var party = Server.GetRegisteredObject<MapEvent>(mapEventId).DefenderSide.Parties[0].Party.MobileParty;
            var oldTroop = Server.CreateRegisteredObject<CharacterObject>("health_fixture_original");
            var newTroop = Server.CreateRegisteredObject<CharacterObject>("health_fixture_regular");
            party.MemberRoster.Clear();
            party.MemberRoster.AddToCounts(oldTroop, 7, woundedCount: 2, xpChange: 30);
            var original = party.MemberRoster.GetTroopRoster().ToArray();
            MapEventDebugCommands.ProvisionHealthFixtureRoster(party.MemberRoster, newTroop);
            Assert.Equal(1200, party.MemberRoster.GetTroopCount(newTroop));
            Assert.True(party.MemberRoster.TotalHealthyCount > BattleSizeProvider.MaximumBattleSize);
            party.MemberRoster.AddToCounts(newTroop, -1);
            MapEventDebugCommands.RestoreHealthFixtureRosters(
                new Dictionary<MobileParty, TroopRosterElement[]> { [party] = original },
                new Dictionary<Hero, int>());
            var restored = Assert.Single(party.MemberRoster.GetTroopRoster());
            Assert.Same(oldTroop, restored.Character);
            Assert.Equal(original[0].Number, restored.Number);
            Assert.Equal(original[0].WoundedNumber, restored.WoundedNumber);
            Assert.Equal(original[0].Xp, restored.Xp);
        });
    }
}
#endif
