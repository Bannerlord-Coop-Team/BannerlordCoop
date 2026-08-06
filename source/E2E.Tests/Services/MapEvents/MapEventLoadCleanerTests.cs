using GameInterface.Services.MapEvents;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using Xunit.Abstractions;

namespace E2E.Tests.Services.MapEvents;

public class MapEventLoadCleanerTests : MapEventTestBase
{
    public MapEventLoadCleanerTests(ITestOutputHelper output) : base(output) { }

    [Fact]
    public void FinalizePlayerMapEvents_PlayerEvent_ReleasesPartiesAndDestroysReplicatedEvent()
    {
        var mapEventContext = CreateServerMapEvent();
        var heroId = TestEnvironment.CreateRegisteredObject<Hero>();
        RegisterAsPlayerParty("loaded-player", heroId, mapEventContext.AttackerPartyId);

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<MapEvent>(mapEventContext.MapEventId, out var mapEvent));
            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(mapEventContext.AttackerPartyId, out var attacker));
            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(mapEventContext.DefenderPartyId, out var defender));

            Server.Resolve<IMapEventLoadCleaner>().FinalizePlayerMapEvents();

            Assert.Equal(MapEventState.WaitingRemoval, mapEvent.State);
            Assert.Null(attacker.Party.MapEventSide);
            Assert.Null(defender.Party.MapEventSide);
            Assert.False(Server.ObjectManager.TryGetObject<MapEvent>(mapEventContext.MapEventId, out _));
        }, MapEventDisabledMethods);

        foreach (var client in Clients)
        {
            Assert.False(client.ObjectManager.TryGetObject<MapEvent>(mapEventContext.MapEventId, out _));
        }
    }

    [Fact]
    public void FinalizePlayerMapEvents_AiEvent_RemainsActive()
    {
        var mapEventContext = CreateServerMapEvent();

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<MapEvent>(mapEventContext.MapEventId, out var mapEvent));
            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(mapEventContext.AttackerPartyId, out var attacker));
            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(mapEventContext.DefenderPartyId, out var defender));

            Server.Resolve<IMapEventLoadCleaner>().FinalizePlayerMapEvents();

            Assert.False(mapEvent.IsFinalized);
            Assert.Same(mapEvent, attacker.MapEvent);
            Assert.Same(mapEvent, defender.MapEvent);
            Assert.True(Server.ObjectManager.TryGetObject<MapEvent>(mapEventContext.MapEventId, out _));
        }, MapEventDisabledMethods);

        foreach (var client in Clients)
        {
            Assert.True(client.ObjectManager.TryGetObject<MapEvent>(mapEventContext.MapEventId, out _));
        }
    }
}
