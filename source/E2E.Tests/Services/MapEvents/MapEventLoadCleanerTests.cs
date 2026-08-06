using GameInterface.Services.MapEvents;
using E2E.Tests.Util;
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
        string? armyId = null;
        string? followerId = null;

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<MapEvent>(mapEventContext.MapEventId, out var mapEvent));
            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(mapEventContext.AttackerPartyId, out var attacker));
            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(mapEventContext.DefenderPartyId, out var defender));
            var kingdom = GameObjectCreator.CreateInitializedObject<Kingdom>();
            var follower = GameObjectCreator.CreateInitializedObject<MobileParty>();
            var army = new Army(kingdom, attacker, Army.ArmyTypes.Raider);
            follower.Army = army;
            follower.AttachedTo = attacker;
            follower.Ai.RethinkAtNextHourlyTick = false;
            Assert.True(Server.ObjectManager.TryGetId(army, out armyId));
            Assert.True(Server.ObjectManager.TryGetId(follower, out followerId));
            Assert.Same(mapEvent, follower.MapEvent);

            Server.Resolve<IMapEventLoadCleaner>().FinalizePlayerMapEvents();

            Assert.Equal(MapEventState.WaitingRemoval, mapEvent.State);
            Assert.Null(attacker.Party.MapEventSide);
            Assert.Null(defender.Party.MapEventSide);
            Assert.Null(follower.Party.MapEventSide);
            Assert.Null(follower.Army);
            Assert.Null(follower.AttachedTo);
            Assert.True(follower.Ai.RethinkAtNextHourlyTick);
            Assert.False(Server.ObjectManager.TryGetObject<MapEvent>(mapEventContext.MapEventId, out _));
            Assert.False(Server.ObjectManager.TryGetObject<Army>(armyId, out _));
        }, MapEventDisabledMethods);

        foreach (var client in Clients)
        {
            Assert.False(client.ObjectManager.TryGetObject<MapEvent>(mapEventContext.MapEventId, out _));
            Assert.False(client.ObjectManager.TryGetObject<Army>(armyId, out _));
            Assert.True(client.ObjectManager.TryGetObject<MobileParty>(followerId, out var follower));
            Assert.Null(follower.Army);
            Assert.Null(follower.AttachedTo);
        }
    }

    [Fact]
    public void FinalizePlayerMapEvents_PlayerFollowerInAiLedArmy_PreservesArmy()
    {
        var mapEventContext = CreateServerMapEvent();
        var (_, playerPartyId) = CreatePlayerHeroParty("loaded-player-follower");
        string? armyId = null;

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<MapEvent>(mapEventContext.MapEventId, out var mapEvent));
            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(mapEventContext.AttackerPartyId, out var armyLeader));
            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(playerPartyId, out var playerFollower));
            var kingdom = GameObjectCreator.CreateInitializedObject<Kingdom>();
            var army = new Army(kingdom, armyLeader, Army.ArmyTypes.Raider);
            playerFollower.Army = army;
            playerFollower.AttachedTo = armyLeader;
            Assert.True(Server.ObjectManager.TryGetId(army, out armyId));
            Assert.Same(mapEvent, playerFollower.MapEvent);

            Server.Resolve<IMapEventLoadCleaner>().FinalizePlayerMapEvents();

            Assert.False(Server.ObjectManager.TryGetObject<MapEvent>(mapEventContext.MapEventId, out _));
            Assert.True(Server.ObjectManager.TryGetObject<Army>(armyId, out _));
            Assert.Same(army, playerFollower.Army);
            Assert.Same(armyLeader, playerFollower.AttachedTo);
        }, MapEventDisabledMethods);

        foreach (var client in Clients)
        {
            Assert.True(client.ObjectManager.TryGetObject<Army>(armyId, out var army));
            Assert.True(client.ObjectManager.TryGetObject<MobileParty>(playerPartyId, out var playerFollower));
            Assert.True(client.ObjectManager.TryGetObject<MobileParty>(mapEventContext.AttackerPartyId, out var armyLeader));
            Assert.Same(army, playerFollower.Army);
            Assert.Same(armyLeader, playerFollower.AttachedTo);
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
