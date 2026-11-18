using E2E.Tests.Util;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using Xunit.Abstractions;

namespace E2E.Tests.Services.MapEvents;

/// <summary>
/// v1.5 keeps a battle's settlement on its map event component, so clients get it with the component.
/// </summary>
public class MapEventComponentSyncTests : MapEventTestBase
{
    public MapEventComponentSyncTests(ITestOutputHelper output) : base(output) { }

    [Fact]
    public void SiegeAssaultComponent_ReplicatesWithItsSettlement()
    {
        string? mapEventId = null;
        string? settlementId = null;
        // The siege steps read a live SiegeEvent, which this battle has none of
        var disabledMethods = MapEventDisabledMethods
            .Append(AccessTools.Method(typeof(SiegeAssaultEventComponent), nameof(SiegeAssaultEventComponent.UpdateMapEventSettlement)))
            .Append(AccessTools.Method(typeof(SiegeAssaultEventComponent), nameof(SiegeAssaultEventComponent.AddMapEventParties)))
            .Append(AccessTools.Method(typeof(SiegeAssaultEventComponent), nameof(SiegeAssaultEventComponent.AddInsideSettlementParties)))
            .ToList();

        Server.Call(() =>
        {
            var settlement = GameObjectCreator.CreateInitializedObject<Settlement>();
            var attacker = GameObjectCreator.CreateInitializedObject<MobileParty>();
            var defender = GameObjectCreator.CreateInitializedObject<MobileParty>();
            var mapEvent = GameObjectCreator.CreateInitializedObject<MapEvent>();
            mapEvent.MapEventVisual = MockMapEventVisual();
            var component = SiegeAssaultEventComponent.CreateComponentForOldSaves(mapEvent);
            component.MapEventSettlement = settlement;
            mapEvent.Initialize(attacker.Party, defender.Party, component);
            mapEvent.MapEventVisual = null;

            Assert.True(Server.ObjectManager.TryGetId(mapEvent, out mapEventId));
            Assert.True(Server.ObjectManager.TryGetId(settlement, out settlementId));
        }, disabledMethods);
        TestEnvironment.FlushCoalescer();

        foreach (var client in Clients)
        {
            client.Call(() =>
            {
                Assert.True(client.ObjectManager.TryGetObject<MapEvent>(mapEventId!, out var mapEvent));
                Assert.True(client.ObjectManager.TryGetObject<Settlement>(settlementId!, out var settlement));
                var component = Assert.IsType<SiegeAssaultEventComponent>(mapEvent.Component);
                Assert.Same(settlement, component.MapEventSettlement);
            }, disabledMethods);
        }
    }
}
