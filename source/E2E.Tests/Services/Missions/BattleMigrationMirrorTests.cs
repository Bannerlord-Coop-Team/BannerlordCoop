using System;
using System.Linq;
using Common.Messaging;
using E2E.Tests.Environment;
using E2E.Tests.Environment.MockEngine;
using GameInterface.Services.MapEvents;
using GameInterface.Services.MapEvents.TroopSupply;
using Missions;
using Missions.Battles;
using Missions.Messages;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;
using Xunit;
using Xunit.Abstractions;

namespace E2E.Tests.Services.Missions;

/// <summary>
/// Phase C: host migration adoption. When the old host leaves and this client is promoted, the new host adopts
/// the departed host's agents — taking authority, converting the inert puppets to AI combatants, and (because
/// no general commands them in a coop battle) issuing an explicit Charge so they engage. This is the path of
/// the live "migrated NPCs stand still" bug; the mirror asserts the wiring (controller, formation, order) is
/// correct, isolating any remaining issue to the native AI itself.
/// </summary>
public class BattleMigrationMirrorTests : MissionTestEnvironment
{
    public BattleMigrationMirrorTests(ITestOutputHelper output) : base(output) { }

    [Fact]
    public void HostMigration_AdoptsNpcPuppets_AsAi_InFormation_OrderedToCharge()
    {
        using var fixture = new MissionEngineFixture();
        var newHost = Clients.First();
        SetControllerId(newHost, "B");

        var npcId = Guid.NewGuid();

        newHost.Call(() =>
        {
            var mock = fixture.CreateMission(newHost);
            var controller = newHost.Resolve<CoopBattleController>();
            var registry = newHost.Resolve<INetworkAgentRegistry>();

            // Put this controller "in" battle mapEvent1 so the migration handler accepts the message.
            controller.Session.TryBegin("mapEvent1");

            // An enemy NPC the OLD host "A" was running, replicated here as an inert puppet on an Attacker team.
            var team = new MockTeam(BattleSideEnum.Attacker);
            BasicCharacterObject character = Game.Current.PlayerTroop;
            var npc = mock.SpawnAgent(new AgentBuildData(character).Controller(AgentControllerType.None).Team(team.Shell));
            Assert.True(registry.TryRegisterAgent("A", npcId, npc)); // owned by the old host

            // Old host A departed; the server promoted us (B). Adopt A's agents.
            newHost.Resolve<IMessageBroker>().Publish(this, new BattleHostMigrated("mapEvent1", "A"));

            // Adopted: authority moved to us, the inert puppet is now AI-controlled and placed in an
            // AI-controlled formation. This is the migration "NPCs stand still" path — the harness proves the
            // adoption + AI conversion + formation assignment + AI-control + authority transfer are all correct,
            // which narrows any remaining live issue to the engine AI itself. The coop code's final step (issuing
            // the formation a Charge order) can't be exercised headless — MovementOrder construction needs the
            // live game (see MissionEngineFixture) — so it is the one part outside this test's reach.
            Assert.True(AgentMirror.TryGet(npc, out var mirror));
            Assert.Equal(AgentControllerType.AI, mirror.Controller);
            Assert.NotNull(mirror.Formation);
            Assert.True(MockFormation.ForShell(mirror.Formation, out var formation));
            Assert.True(formation.IsAIControlled);

            Assert.True(registry.TryGetAgentInfo(npcId, out var info));
            Assert.Equal("B", info.CurrentAuthority);

            GC.KeepAlive(controller);
        });
    }

    /// <summary>
    /// A host RETREAT is not a disconnect: the retreating host's OWN party withdraws (despawned on every
    /// client), while the AI it was running is adopted by the promoted successor — two DISJOINT sets, which is
    /// what makes the despawn and the adoption race-free (racing over the same agents was a native crash).
    /// Own-party membership is identified by the agent's origin party, not its battle side, so a host-fielded
    /// allied NPC party keeps fighting.
    /// </summary>
    [Fact]
    public void HostRetreat_DespawnsItsOwnParty_AndAdoptsOnlyTheAiItRan()
    {
        using var fixture = new MissionEngineFixture();
        var (mapEventId, partyIds) = SetupCoopBattle("A", "B");
        var successor = Clients.Skip(1).First(); // "B"

        var ownPartyTroopId = Guid.NewGuid();
        var npcTroopId = Guid.NewGuid();

        try
        {
            successor.Call(() =>
            {
                var mock = fixture.CreateMission(successor);
                var controller = successor.Resolve<CoopBattleController>();
                var registry = successor.Resolve<INetworkAgentRegistry>();

                controller.Session.TryBegin(mapEventId);
                successor.Resolve<IBattleHostRegistry>().Set(mapEventId, new BattleHostAssignment("A", new[] { "B" }));

                // A live coop battle: the movement handler's location-style peer cleanup must stand down (it is
                // gated on the spawn gate), exactly as in a real battle — otherwise it would despawn A's agents
                // wholesale and there would be nothing left to adopt.
                BattleSpawnGate.BeginBattle(mapEventId, 1000);

                Assert.True(successor.ObjectManager.TryGetObject<MobileParty>(partyIds[0], out var hostParty));
                var character = (CharacterObject)Game.Current.PlayerTroop;
                var team = new MockTeam(BattleSideEnum.Defender);

                // A's OWN-party troop (origin party = A's player party) — must withdraw on A's retreat.
                var ownOrigin = new CoopAgentOrigin(character, hostParty.Party, -1, null, new UniqueTroopDescriptor(1));
                var ownTroop = mock.SpawnAgent(new AgentBuildData(character).Controller(AgentControllerType.None).Team(team.Shell).TroopOrigin(ownOrigin));
                Assert.True(registry.TryRegisterAgent("A", ownPartyTroopId, ownTroop));

                // An NPC troop A was RUNNING (no player origin party) — must be adopted and keep fighting.
                var npcTroop = mock.SpawnAgent(new AgentBuildData(character).Controller(AgentControllerType.None).Team(team.Shell));
                Assert.True(registry.TryRegisterAgent("A", npcTroopId, npcTroop));

                // A retreats (the graceful leave arrives first), then the server promotes us.
                successor.Resolve<IMessageBroker>().Publish(this, new MissionPeerLeft("A", mapEventId));
                successor.Resolve<IMessageBroker>().Publish(this, new BattleHostMigrated(mapEventId, "A"));

                // A's own-party troop withdrew: faded out and deregistered, NOT adopted.
                Assert.False(registry.TryGetAgentInfo(ownPartyTroopId, out _));
                Assert.True(AgentMirror.TryGet(ownTroop, out var ownMirror));
                Assert.False(ownMirror.IsActive);

                // The NPC troop was adopted: authority moved to us and it fights on as host AI.
                Assert.True(registry.TryGetAgentInfo(npcTroopId, out var npcInfo));
                Assert.Equal("B", npcInfo.CurrentAuthority);
                Assert.True(AgentMirror.TryGet(npcTroop, out var npcMirror));
                Assert.Equal(AgentControllerType.AI, npcMirror.Controller);

                GC.KeepAlive(controller);
            });
        }
        finally
        {
            BattleSpawnGate.EndBattle();
        }
    }
}
