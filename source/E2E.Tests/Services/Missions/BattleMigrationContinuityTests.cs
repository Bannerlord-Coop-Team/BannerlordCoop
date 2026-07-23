using System;
using System.Linq;
using Common.Messaging;
using E2E.Tests.Environment.MockEngine;
using GameInterface.Services.MapEvents;
using GameInterface.Services.MapEvents.TroopSupply;
using Missions;
using Missions.Battles;
using Missions.Messages;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;
using Xunit.Abstractions;

namespace E2E.Tests.Services.Missions;

/// <summary>
/// BR-016 Host Migration Continuity: a host migration shall not restart the battle mission, respawn defeated
/// troops, or reset battle progress. This asserts the two facets that together make migration seamless — the
/// promoted host adopts the previous host's agents IN PLACE (the same agent object, its health/state and
/// registration unchanged, no respawn) AND the troop supplier a new owner receives resumes from the server's
/// supplied pointer, so already-supplied troops are not re-supplied. Mirrors the BattleMigrationMirrorTests
/// engine-fixture pattern for the agent facet and the CoopTroopSupplier pattern for the supplier facet.
/// </summary>
public class BattleMigrationContinuityTests : MissionTestEnvironment
{
    public BattleMigrationContinuityTests(ITestOutputHelper output) : base(output) { }

    [Fact]
    [Trait("Requirement", "BR-016")]
    public void HostMigration_AdoptsAgentsInPlace_WithoutRespawnOrReset_AndSupplierResumesFromPointer()
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

            // An agent the OLD host "A" was running, replicated here as an inert puppet, already damaged (i.e.
            // mid-battle progress). Holding this exact agent lets us prove it is adopted in place, not respawned.
            var team = new MockTeam(BattleSideEnum.Attacker);
            BasicCharacterObject character = Game.Current.PlayerTroop;
            var npc = mock.SpawnAgent(new AgentBuildData(character).Controller(AgentControllerType.None).Team(team.Shell));
            Assert.True(registry.TryRegisterAgent("A", npcId, npc)); // owned by the old host

            Assert.True(AgentMirror.TryGet(npc, out var preMirror));
            preMirror.Health = 42f; // battle damage taken before the migration

            // Old host A departed; the server promoted us (B). Adopt A's agents.
            newHost.Resolve<IMessageBroker>().Publish(this, new BattleHostMigrated("mapEvent1", "A"));

            // Continuity: the SAME agent is still registered (not respawned) under the same id, its assignment
            // (OriginalOwner) preserved, and only CONTROL moved to the new host.
            Assert.True(registry.TryGetAgentInfo(npcId, out var info));
            Assert.Same(npc, info.Agent);          // not re-created — adopted in place
            Assert.Equal("B", info.CurrentAuthority);
            Assert.Equal("A", info.OriginalOwner);

            // Battle progress not reset: health is unchanged (not restored to full) and the agent is neither
            // despawned nor killed by the migration.
            Assert.True(AgentMirror.TryGet(npc, out var mirror));
            Assert.Equal(42f, mirror.Health);
            Assert.True(mirror.IsActive);
            Assert.False(mirror.WasKilled);

            GC.KeepAlive(controller);
        });

        // Supplier continuity: on migration a new owner is handed the reserve at the server's supplied pointer,
        // so it resumes mid-list and never re-supplies troops the departed owner already spawned.
        var supplier = new CoopTroopSupplier("mapEvent1", BattleSideEnum.Attacker, null, new BattleAgentBudget());
        supplier.SetReserve(new[] { new PartyReserve("P", 4, Entries(10)) }); // server pointer = 4

        Assert.Equal(4, supplier.GetSuppliedByParty().Single().supplied); // resumes at the server's pointer
        Assert.Equal(6, supplier.NumTroopsNotSupplied);

        supplier.SupplyTroops(2); // supplies entries 4 and 5 (the tail) — never re-emitting 0..3
        Assert.Equal(6, supplier.GetSuppliedByParty().Single().supplied);
        Assert.Equal(4, supplier.NumTroopsNotSupplied);
    }

    private static TroopReserveEntry[] Entries(int count, int seedBase = 500)
    {
        var entries = new TroopReserveEntry[count];
        for (int i = 0; i < count; i++)
            entries[i] = new TroopReserveEntry(seedBase + i, $"Char_{i}", formationClass: 0);
        return entries;
    }
}
