using Common.Messaging;
using Common.Network;
using Common.Util;
using E2E.Tests.Environment.Instance;
using E2E.Tests.Services.Missions;
using GameInterface.Services.MapEvents;
using GameInterface.Services.MapEvents.Handlers;
using GameInterface.Services.MapEvents.Messages.Start;
using GameInterface.Services.Players;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;
using Xunit.Abstractions;

namespace E2E.Tests.Services.MapEvents;

/// <summary>
/// End-to-end coverage of battle instance creation (BR-002): when a player selects the playable battle
/// option the server creates — or identifies, if one already exists — the battle instance record for the map
/// event, keyed by its unique mission identifier (BR-104) and associated with the map event. The battle
/// mission runs on the participating clients (the server broadcasts the start rather than opening a mission
/// itself). The instance record in this stack is the server-authoritative host assignment
/// (<see cref="IBattleHostRegistry"/>), keyed by the map-event object-manager id.
/// </summary>
public class BattleInstanceCreationTests : MissionTestEnvironment
{
    public BattleInstanceCreationTests(ITestOutputHelper output) : base(output) { }

    /// <summary>
    /// BR-002: the first player to enter the battle causes the server to CREATE the battle instance record,
    /// keyed by the map event's unique id, and replicate it to every instance — the record's key is the same
    /// server-assigned map-event id everywhere (its association with the map event).
    /// </summary>
    [Fact]
    [Trait("Requirement", "BR-002")]
    public void FirstPlayerEntersBattle_ServerCreatesInstanceRecordKeyedByMapEventId_OnAllInstances()
    {
        var (mapEventId, _) = SetupCoopBattle("ctrl-A", "ctrl-B");

        EnterBattle(Clients.First(), mapEventId); // ctrl-A selects the playable battle -> instance record created

        AssertBattleInstanceRecord(Server, mapEventId, "ctrl-A");
        foreach (var client in Clients)
            AssertBattleInstanceRecord(client, mapEventId, "ctrl-A");
    }

    /// <summary>
    /// BR-002: a second player entering the SAME map event IDENTIFIES the existing battle instance record
    /// rather than creating a new one — the host is unchanged, the entrant is appended to the same record, and
    /// the whole thing stays keyed by the one unchanged map-event id (no duplicate instance is minted).
    /// </summary>
    [Fact]
    [Trait("Requirement", "BR-002")]
    public void SecondPlayerEntersSameEvent_IdentifiesExistingInstanceRecord_WithoutCreatingADuplicate()
    {
        var (mapEventId, _) = SetupCoopBattle("ctrl-A", "ctrl-B");

        EnterBattle(Clients.First(), mapEventId); // creates the instance record (host ctrl-A)
        EnterBattle(Clients.Last(), mapEventId);  // must identify the existing one, not create a second

        AssertBattleInstanceRecord(Server, mapEventId, "ctrl-A", "ctrl-B");
        foreach (var client in Clients)
            AssertBattleInstanceRecord(client, mapEventId, "ctrl-A", "ctrl-B");
    }

    /// <summary>
    /// BR-002: a mission-mode <see cref="NetworkBattleStartRequest"/> for a fresh field battle broadcasts the
    /// mission start carrying the map-event id and claims the mission mode — the mission is handed to the
    /// participating clients (the server does not run it). Marked uncertain only because the accept path drives
    /// native <c>MapEventSide.MakeReadyForMission</c> on a headless field battle; the assertions are the real
    /// BR-002 contract regardless.
    /// </summary>
    [Fact]
    [Trait("Requirement", "BR-002")]
    public void MissionModeStart_BroadcastsAttackMissionCarryingMapEventId_AndClaimsMissionMode()
    {
        var (mapEventId, partyIds) = SetupCoopBattle("ctrl-A", "ctrl-B");
        var troopId = CreateRegisteredObject<CharacterObject>();
        var clients = Clients.ToArray();
        var client = clients[0];
        Server.Resolve<IPlayerManager>().SetPeer("ctrl-A", clients[0].NetPeer);
        Server.Resolve<IPlayerManager>().SetPeer("ctrl-B", clients[1].NetPeer);

        // Give both sides some troops so the native make-ready step has something to field (mirrors the working
        // village-raid mission-start path). Seeded server-side only (that is where the handler makes sides ready).
        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(partyIds[0], out var attacker));
            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(partyIds[1], out var defender));
            Assert.True(Server.ObjectManager.TryGetObject<CharacterObject>(troopId, out var troop));
            using (new AllowedThread())
            {
                attacker.MemberRoster.AddToCounts(troop, 5);
                defender.MemberRoster.AddToCounts(troop, 5);
            }
        }, MapEventDisabledMethods);

        try
        {
            Server.NetworkSentMessages.Clear();
            var reservedControllers = new HashSet<string>();
            Server.Resolve<IMessageBroker>().Subscribe<BattleJoinAccepted>(payload =>
            {
                if (payload.What.InstanceId == mapEventId)
                    reservedControllers.Add(payload.What.ControllerId);
            });

            client.Call(() => client.Resolve<INetwork>().SendAll(new NetworkBattleStartRequest(
                Guid.NewGuid().ToString(),
                (int)BattleStartMode.Mission,
                mapEventId,
                partyIds[0])), MapEventDisabledMethods);

            // The server hands the mission to the clients: it broadcasts the attack-mission start carrying the
            // battle's unique map-event id (BR-104), rather than opening a mission itself (BR-002 para 2).
            var start = Assert.Single(Server.NetworkSentMessages.GetMessages<NetworkStartAttackMission>());
            Assert.Equal(mapEventId, start.MapEventId);

            // The event is claimed for the mission mode (so the auto-resolve option is greyed everywhere).
            var mode = Assert.Single(Server.NetworkSentMessages.GetMessages<NetworkBattleModeSet>());
            Assert.Equal(mapEventId, mode.MapEventId);
            Assert.Equal((int)BattleStartMode.Mission, mode.Mode);

            var reply = Assert.Single(Server.NetworkSentMessages.GetMessages<NetworkBattleStartReply>());
            Assert.True(reply.Accepted);
            Assert.Equal(new[] { "ctrl-A", "ctrl-B" }, reservedControllers.OrderBy(id => id));
        }
        finally
        {
            Server.Call(() => ServerBattleModeArbiter.Release(mapEventId));
        }
    }

    /// <summary>
    /// Asserts the battle instance record on <paramref name="instance"/> is keyed by <paramref name="mapEventId"/>
    /// with the given host/successor line, and that the map event it names still resolves by — and round-trips
    /// back to — that same id (its association with the map event).
    /// </summary>
    private void AssertBattleInstanceRecord(EnvironmentInstance instance, string mapEventId, string expectedHost, params string[] expectedSuccessors)
    {
        AssertHost(instance, mapEventId, expectedHost, expectedSuccessors);
        instance.Call(() =>
        {
            Assert.True(instance.ObjectManager.TryGetObject<MapEvent>(mapEventId, out var mapEvent),
                $"map event {mapEventId} should resolve on {instance.GetType().Name}");
            Assert.True(instance.ObjectManager.TryGetId(mapEvent, out var roundTripId));
            Assert.Equal(mapEventId, roundTripId);
        });
    }
}
