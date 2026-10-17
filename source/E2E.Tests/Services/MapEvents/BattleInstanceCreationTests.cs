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

            // The server hands the mission to each authoritative participant, carrying the battle's unique
            // map-event id (BR-104), rather than opening a mission itself (BR-002 para 2).
            var starts = Server.NetworkSentMessages.GetMessages<NetworkStartAttackMission>().ToArray();
            Assert.Equal(2, starts.Length);
            Assert.All(starts, start => Assert.Equal(mapEventId, start.MapEventId));

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
}
