using System.Collections.Generic;
using System.Linq;
using GameInterface.Services.MapEvents.Interfaces;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.GameComponents;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;
using Xunit;
using Xunit.Abstractions;

namespace E2E.Tests.Services.MapEvents;

public class MapEventResultCaptureTests : MapEventTestBase
{
    public MapEventResultCaptureTests(ITestOutputHelper output) : base(output) { }

    [Fact]
    public void CalculateAndCommitMapEventResults_NoCaptorAvailable_CompletesWithoutPrisoner()
    {
        var context = CreateServerMapEvent();
        var troopId = TestEnvironment.CreateRegisteredObject<CharacterObject>();

        var disabledMethods = MapEventDisabledMethods
            .Append(AccessTools.Method(typeof(MapEventResultsInterface), "LootDefeatedPartyCasualties"))
            .Append(AccessTools.Method(typeof(MapEventResultsInterface), "LootDefeatedPartyItems"))
            .Append(AccessTools.Method(typeof(MapEventResultsInterface), "LootDefeatedPartyPrisoners"))
            .Append(AccessTools.Method(typeof(MapEvent), "LootDefeatedPartyShips"))
            .Append(AccessTools.Method(typeof(MapEvent), "CommitCalculatedMapEventResults"))
            .Append(AccessTools.Method(typeof(MapEvent), "FindWinnerPartyToGetCurrentLootObjectBasedOnChances"))
            .ToList();

        Server.Call(() =>
        {
            Campaign.Current._gameModels = new GameModels(
                new List<GameModel> { new DefaultBattleRewardModel() });

            Assert.True(Server.ObjectManager.TryGetObject<MapEvent>(context.MapEventId, out var mapEvent));
            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(context.AttackerPartyId, out var attacker));
            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(context.DefenderPartyId, out var defender));
            Assert.True(Server.ObjectManager.TryGetObject<CharacterObject>(troopId, out var troop));

            attacker.MemberRoster.AddToCounts(troop, 1);
            defender.MemberRoster.AddToCounts(troop, 1);
            mapEvent._battleState = BattleState.AttackerVictory;

            Server.Resolve<IMapEventResultsInterface>()
                .CalculateAndCommitMapEventResults(mapEvent, out var playerLootData);

            Assert.True(mapEvent._mapEventResultsApplied);
            Assert.Equal(0, defender.MemberRoster.TotalManCount);
            Assert.Equal(0, attacker.PrisonRoster.TotalManCount);
            Assert.Empty(playerLootData.LootedPrisoners);
        }, disabledMethods);
    }
}
