using E2E.Tests.Environment;
using E2E.Tests.Util;
using GameInterface.Services.MapEvents.Messages.Start;
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
        // The fixture supplies the siege graph and starts the battle with only its two leaders.
        var disabledMethods = MapEventDisabledMethods
            .Append(AccessTools.Method(typeof(SiegeAssaultEventComponent), nameof(SiegeAssaultEventComponent.AddMapEventParties)))
            .Append(AccessTools.Method(typeof(SiegeAssaultEventComponent), nameof(SiegeAssaultEventComponent.AddInsideSettlementParties)))
            .Concat(SiegeCreationDisabledMethods)
            .ToList();

        Server.Resolve<TestNetworkRouter>().ReceiveContext = TestNetworkReceiveContext.PollerThread;
        Server.Call(() =>
        {
            var settlement = GameObjectCreator.CreateInitializedObject<Settlement>();
            var attacker = GameObjectCreator.CreateInitializedObject<MobileParty>();
            var defender = GameObjectCreator.CreateInitializedObject<MobileParty>();
            CreateReplicableSiegeEvent(settlement, attacker);
            var component = SiegeAssaultEventComponent.CreateSiegeAssaultMapEvent(attacker.Party, defender.Party);
            var mapEvent = component.MapEvent;

            Assert.True(Server.ObjectManager.TryGetId(mapEvent, out mapEventId));
            Assert.True(Server.ObjectManager.TryGetId(settlement, out settlementId));
        }, disabledMethods);

        var commit = Assert.Single(Server.NetworkSentMessages.GetMessages<NetworkMapEventInitialized>());
        Assert.False(commit.IsTerminal);

        foreach (var client in Clients)
        {
            client.Call(() =>
            {
                client.PumpGameThread();
                Assert.True(client.ObjectManager.TryGetObject<MapEvent>(mapEventId!, out var mapEvent));
                Assert.True(client.ObjectManager.TryGetObject<Settlement>(settlementId!, out var settlement));
                var component = Assert.IsType<SiegeAssaultEventComponent>(mapEvent.Component);
                Assert.Same(settlement, component.MapEventSettlement);
                Assert.Contains(mapEvent, Campaign.Current.MapEventManager.MapEvents);
            }, disabledMethods);
        }
    }
}
