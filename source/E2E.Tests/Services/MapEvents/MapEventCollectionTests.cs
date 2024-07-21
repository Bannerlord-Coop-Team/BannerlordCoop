using Common.Util;
using E2E.Tests.Environment;
using E2E.Tests.Util;
using HarmonyLib;
using SandBox.GauntletUI.Map;
using System.Reflection;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using Xunit.Abstractions;

namespace E2E.Tests.Services.MapEvents;
public class MapEventCollectionTests : IDisposable
{
    E2ETestEnvironment TestEnvironment { get; }

    public MapEventCollectionTests(ITestOutputHelper output)
    {
        TestEnvironment = new E2ETestEnvironment(output);
    }

    public void Dispose()
    {
        TestEnvironment.Dispose();
    }

    [Fact]
    public void Server_MapEvent_Sides_SyncAllClients()
    {
        // TODO get working in docker image

        //// Arrange
        //var server = TestEnvironment.Server;

        //// Act
        //string? attackerSideId = null;
        //string? defenderSideId = null;
        //server.Call(() =>
        //{
        //    var mapEvent = GameObjectCreator.CreateInitializedObject<MapEvent>();
        //    var attackerParty = GameObjectCreator.CreateInitializedObject<MobileParty>();
        //    var defenderParty = GameObjectCreator.CreateInitializedObject<MobileParty>();

        //    // TODO find better way
        //    mapEvent.MapEventVisual = ObjectHelper.SkipConstructor<GauntletMapEventVisual>();

        //    mapEvent.Initialize(attackerParty.Party, defenderParty.Party);

        //    Assert.True(server.ObjectManager.TryGetId(attackerParty, out attackerSideId));
        //    Assert.True(server.ObjectManager.TryGetId(defenderParty, out defenderSideId));
        //}, new MethodBase[] { AccessTools.Method(typeof(GauntletMapEventVisual), nameof(GauntletMapEventVisual.Initialize)) });

        //// Assert
        //Assert.NotNull(attackerSideId);
        //Assert.NotNull(defenderSideId);

        //foreach (var client in TestEnvironment.Clients)
        //{
        //    Assert.True(client.ObjectManager.TryGetObject<MapEventSide>(attackerSideId, out var _));
        //    Assert.True(client.ObjectManager.TryGetObject<MapEventSide>(defenderSideId, out var _));
        //}
    }

    [Fact]
    public void Client_MapEvent_Sides_DoesNothing()
    {
        // TODO get working in docker image

        //// Arrange
        //var server = TestEnvironment.Server;

        //string? mapEventId = null;
        //string? attackerPartyId = null;
        //string? defenderPartyId = null;
        //server.Call(() =>
        //{
        //    var mapEvent = GameObjectCreator.CreateInitializedObject<MapEvent>();
        //    var attackerParty = GameObjectCreator.CreateInitializedObject<MobileParty>();
        //    var defenderParty = GameObjectCreator.CreateInitializedObject<MobileParty>();

        //    Assert.True(server.ObjectManager.TryGetId(mapEvent, out mapEventId));
        //    Assert.True(server.ObjectManager.TryGetId(attackerParty, out attackerPartyId));
        //    Assert.True(server.ObjectManager.TryGetId(defenderParty, out defenderPartyId));
        //});

        //// Act
        //string? attackerSideId = null;
        //string? defenderSideId = null;

        //var firstClient = TestEnvironment.Clients.First();
        //firstClient.Call(() =>
        //{
        //    Assert.True(firstClient.ObjectManager.TryGetObject<MobileParty>(attackerPartyId, out var attackerParty));
        //    Assert.True(firstClient.ObjectManager.TryGetObject<MobileParty>(defenderPartyId, out var defenderParty));
        //    Assert.True(firstClient.ObjectManager.TryGetObject<MapEvent>(mapEventId, out var mapEvent));

        //    // TODO find better way
        //    mapEvent.MapEventVisual = ObjectHelper.SkipConstructor<GauntletMapEventVisual>();

        //    mapEvent.Initialize(attackerParty.Party, defenderParty.Party);

        //    Assert.False(server.ObjectManager.TryGetId(mapEvent.AttackerSide, out attackerSideId));
        //    Assert.False(server.ObjectManager.TryGetId(mapEvent.DefenderSide, out defenderSideId));
        //}, new MethodBase[] { AccessTools.Method(typeof(GauntletMapEventVisual), nameof(GauntletMapEventVisual.Initialize)) });

        //// Assert
        //Assert.Null(attackerSideId);
        //Assert.Null(defenderSideId);
    }
}
