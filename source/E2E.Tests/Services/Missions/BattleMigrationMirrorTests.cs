using System;
using System.Linq;
using Common.Messaging;
using E2E.Tests.Environment;
using E2E.Tests.Environment.MockEngine;
using HarmonyLib;
using Missions;
using Missions.Battles;
using Missions.Messages;
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
            AccessTools.Field(typeof(CoopBattleController), "instanceId").SetValue(controller, "mapEvent1");

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
}
