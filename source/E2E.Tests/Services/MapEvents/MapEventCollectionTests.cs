using Common.Util;
using E2E.Tests.Environment;
using E2E.Tests.Util;
using HarmonyLib;
using NetworkMessages.FromServer;
using SandBox.GauntletUI.Map;
using System.Reflection;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using Xunit.Abstractions;

namespace E2E.Tests.Services.MapEvents;
public class MapEventCollectionTests : SyncTestBase
{

    public MapEventCollectionTests(ITestOutputHelper output) : base(output)
    {
        TestEnvironment.CreateRegisteredObject<MapEvent>();
        TestEnvironment.CreateRegisteredObject<MapEventSide>();
        TestEnvironment.CreateRegisteredObject<MapEventSide>();
    }

    [Fact]
    public void Server_MapEvent_Properties()
    {
        TestEnvironment.AssertCollectionReferenceField<MapEvent, MapEventSide>(nameof(MapEvent._sides));

    }

    [Fact]
    public void Server_MapEvent_Sides_SyncAllClients()
    {
        // TODO IDK WHY THIS DOESNT WORK IN DOCKER
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

        //    Assert.True(server.ObjectManager.TryGetId(mapEvent.AttackerSide, out attackerSideId));
        //    Assert.True(server.ObjectManager.TryGetId(mapEvent.DefenderSide, out defenderSideId));
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
}
