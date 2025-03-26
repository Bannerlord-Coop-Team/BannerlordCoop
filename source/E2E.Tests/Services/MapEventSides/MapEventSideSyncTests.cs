using E2E.Tests.Environment;
using E2E.Tests.Util;
using GameInterface.Services.MapEventSides.Patches;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;
using Xunit.Abstractions;

namespace E2E.Tests.Services.MapEventSides;
public class MapEventSideSyncTests : IDisposable
{
    E2ETestEnvironment TestEnvironment { get; }

    public MapEventSideSyncTests(ITestOutputHelper output)
    {
        TestEnvironment = new E2ETestEnvironment(output);
    }

    public void Dispose()
    {
        TestEnvironment.Dispose();
    }

    [Fact]
    public void Server_MapEventSide_ChangeLeaderParty_SyncAllClients()
    {
        // Arrange
        var server = TestEnvironment.Server;

        var casualtiesField = AccessTools.Field(typeof(MapEventSide), nameof(MapEventSide.Casualties));
        var influenceField = AccessTools.Field(typeof(MapEventSide), nameof(MapEventSide.InfluenceValue));
        var surrenderedField = AccessTools.Field(typeof(MapEventSide), nameof(MapEventSide.IsSurrendered));
        var leaderSimField = AccessTools.Field(typeof(MapEventSide), nameof(MapEventSide.LeaderSimulationModifier));
        var renownMapEndField = AccessTools.Field(typeof(MapEventSide), nameof(MapEventSide.RenownAtMapEventEnd));
        var renownField = AccessTools.Field(typeof(MapEventSide), nameof(MapEventSide.RenownValue));
        var strengthField = AccessTools.Field(typeof(MapEventSide), nameof(MapEventSide.StrengthRatio));
        var mapEventField = AccessTools.Field(typeof(MapEventSide), nameof(MapEventSide._mapEvent));
        var troopCacheField = AccessTools.Field(typeof(MapEventSide), nameof(MapEventSide._requiresTroopCacheUpdate));
        var selectTroopField = AccessTools.Field(typeof(MapEventSide), nameof(MapEventSide._selectedSimulationTroop));
        var selectedIndexField = AccessTools.Field(typeof(MapEventSide), nameof(MapEventSide._selectedSimulationTroopIndex));

        var casualtiesIntercept = TestEnvironment.GetIntercept(casualtiesField);
        var influenceIntercept = TestEnvironment.GetIntercept(influenceField);
        var surrenderedIntercept = TestEnvironment.GetIntercept(surrenderedField);
        var leaderSimIntercept = TestEnvironment.GetIntercept(leaderSimField);
        var renownMapEndIntercept = TestEnvironment.GetIntercept(renownMapEndField);
        var renownIntercept = TestEnvironment.GetIntercept(renownField);
        var strengthIntercept = TestEnvironment.GetIntercept(strengthField);
        var mapEventIntercept = TestEnvironment.GetIntercept(mapEventField);
        var troopCacheIntercept = TestEnvironment.GetIntercept(troopCacheField);
        var selectTroopIntercept = TestEnvironment.GetIntercept(selectTroopField);
        var selectedIndexIntercept = TestEnvironment.GetIntercept(selectedIndexField);

        // Act
        string? mapEventSideId = null;
        string? leaderPartyId = null;
        string? mapEventId = null;
        string? kingdomId = null;
        string? characterId = null;
        server.Call(() =>
        {
            var mapEventSide = GameObjectCreator.CreateInitializedObject<MapEventSide>();
            var mobileParty = GameObjectCreator.CreateInitializedObject<MobileParty>();
            var mapEvent = GameObjectCreator.CreateInitializedObject<MapEvent>();
            var kingdom = GameObjectCreator.CreateInitializedObject<Kingdom>();
            var character = GameObjectCreator.CreateInitializedObject<CharacterObject>();

            Assert.True(server.ObjectManager.TryGetId(mapEventSide, out mapEventSideId));
            Assert.True(server.ObjectManager.TryGetId(mobileParty, out leaderPartyId));
            Assert.True(server.ObjectManager.TryGetId(mapEvent, out mapEventId));
            Assert.True(server.ObjectManager.TryGetId(kingdom, out kingdomId));
            Assert.True(server.ObjectManager.TryGetId(character, out characterId));

            mapEventSide.LeaderParty = mobileParty.Party;
            mapEventSide.CasualtyStrength = 5f;
            mapEventSide.MissionSide = BattleSideEnum.Attacker;

            casualtiesIntercept.Invoke(null, new object[] { mapEventSide, 5 });
            influenceIntercept.Invoke(null, new object[] { mapEventSide, 5f });
            surrenderedIntercept.Invoke(null, new object[] { mapEventSide, true });
            leaderSimIntercept.Invoke(null, new object[] { mapEventSide, 5f });
            renownMapEndIntercept.Invoke(null, new object[] { mapEventSide, 5f });
            renownIntercept.Invoke(null, new object[] { mapEventSide, 5f });
            strengthIntercept.Invoke(null, new object[] { mapEventSide, 5f });
            mapEventIntercept.Invoke(null, new object[] { mapEventSide, mapEvent });
            MapEventSideDataPatches.MapFactionIntercept(mapEventSide, kingdom);
            troopCacheIntercept.Invoke(null, new object[] { mapEventSide, true });
            selectTroopIntercept.Invoke(null, new object[] { mapEventSide, character });
            selectedIndexIntercept.Invoke(null, new object[] { mapEventSide, 5 });

        });

        // Assert
        Assert.NotNull(mapEventSideId);

        foreach (var client in TestEnvironment.Clients)
        {
            Assert.True(client.ObjectManager.TryGetObject<MapEventSide>(mapEventSideId, out var mapEventSide));
            Assert.True(client.ObjectManager.TryGetObject<MobileParty>(leaderPartyId, out var leaderParty));
            Assert.True(client.ObjectManager.TryGetObject<MapEvent>(mapEventId, out var mapEvent));
            Assert.True(client.ObjectManager.TryGetObject<Kingdom>(kingdomId, out var mapFaction));
            Assert.True(client.ObjectManager.TryGetObject<CharacterObject>(characterId, out var characterObject));
            
            Assert.Equal(leaderPartyId, mapEventSide.LeaderParty.MobileParty.StringId);
            Assert.Equal(5f, mapEventSide.CasualtyStrength);
            Assert.Equal(BattleSideEnum.Attacker, mapEventSide.MissionSide);

            Assert.Equal(5, mapEventSide.Casualties);
            Assert.Equal(5f, mapEventSide.InfluenceValue);
            Assert.True(mapEventSide.IsSurrendered);
            Assert.Equal(5f, mapEventSide.LeaderSimulationModifier);
            Assert.Equal(5f, mapEventSide.RenownAtMapEventEnd);
            Assert.Equal(5f, mapEventSide.RenownValue);
            Assert.Equal(5f, mapEventSide.StrengthRatio);
            Assert.Equal(mapEvent, mapEventSide._mapEvent);
            Assert.Equal(mapFaction.StringId, mapEventSide._mapFaction.StringId);
            Assert.True(mapEventSide._requiresTroopCacheUpdate);
            Assert.Equal(characterObject, mapEventSide._selectedSimulationTroop);
            Assert.Equal(5, mapEventSide._selectedSimulationTroopIndex);

            //Map faction seperate sync
        }
    }
}
