using System;
using Common.Messaging;
using E2E.Tests.Environment.MockEngine;
using GameInterface.Services.MapEvents;
using Missions;
using Missions.Battles;
using Missions.Messages;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;
using Xunit;
using Xunit.Abstractions;

namespace E2E.Tests.Services.Missions;

/// <summary>
/// BR-031 (Temporary Host Control): when a player disconnects from an active battle mission the mission host
/// temporarily assumes control of the disconnected player's SURVIVING assigned troops. The disconnect arrives
/// server-mediated as <see cref="MissionPeerDisconnected"/>; only the host adopts (the <c>IsLocalHost</c>
/// gate), only surviving (still-registered) agents are taken over, and the assignment — the original owner —
/// is preserved so control can later return. Exercised headless against the <see cref="MissionEngineFixture"/>
/// mock mission, the same adoption core as <c>BattleMigrationMirrorTests</c> but reached through the
/// disconnect trigger rather than host migration.
/// </summary>
public class BattleTemporaryHostControlTests : MissionTestEnvironment
{
    public BattleTemporaryHostControlTests(ITestOutputHelper output) : base(output) { }

    /// <summary>
    /// The host adopts a dropped player's surviving puppet: authority moves to the host, the puppet becomes a
    /// host AI combatant, and the original owner (assignment) is retained. A troop of the same player that had
    /// already been removed (killed and deregistered) before the drop is NOT restored or adopted.
    /// </summary>
    [Fact]
    [Trait("Requirement", "BR-031")]
    public void HostAdoptsDisconnectedPlayersSurvivingTroop_PreservesAssignment_AndSkipsRemovedTroop()
    {
        using var fixture = new MissionEngineFixture();
        var host = Clients.First();
        SetControllerId(host, "H");

        var survivorId = Guid.NewGuid();
        var removedId = Guid.NewGuid();
        var mapEventId = "battle-disconnect";

        try
        {
            host.Call(() =>
            {
                var mock = fixture.CreateMission(host);
                var controller = host.Resolve<CoopBattleController>();
                var registry = host.Resolve<INetworkAgentRegistry>();

                // We are the elected host of this live battle.
                controller.Session.TryBegin(mapEventId);
                host.Resolve<IBattleHostRegistry>().Set(mapEventId, new BattleHostAssignment("H", Array.Empty<string>()));

                // A live coop battle so the location-style peer cleanup (AgentMovementHandler, gated on the
                // spawn gate) stands down — the host adoption is what governs the dropped player's troops.
                BattleSpawnGate.BeginBattle(mapEventId);

                var team = new MockTeam(BattleSideEnum.Attacker);
                BasicCharacterObject character = Game.Current.PlayerTroop;

                // "C" is another player. This is its SURVIVING assigned troop, replicated here as an inert puppet.
                var survivor = mock.SpawnAgent(new AgentBuildData(character).Controller(AgentControllerType.None).Team(team.Shell));
                Assert.True(registry.TryRegisterAgent("C", survivorId, survivor));

                // A troop of C's that had already been removed (killed -> deregistered) BEFORE the disconnect:
                // it is not a surviving troop, so it must not be resurrected or adopted.
                var removed = mock.SpawnAgent(new AgentBuildData(character).Controller(AgentControllerType.None).Team(team.Shell));
                Assert.True(registry.TryRegisterAgent("C", removedId, removed));
                Assert.True(registry.RemoveAgent(removedId));

                // The server reports C dropped ungracefully.
                host.Resolve<IMessageBroker>().Publish(this, new MissionPeerDisconnected("C", mapEventId));

                // The host temporarily assumed control of C's surviving troop: authority is now the host's,
                // the assignment (original owner) is preserved, and the puppet is a host AI combatant.
                Assert.True(registry.TryGetAgentInfo(survivorId, out var info));
                Assert.Equal("H", info.CurrentAuthority);
                Assert.Equal("C", info.OriginalOwner);
                Assert.True(AgentMirror.TryGet(survivor, out var mirror));
                Assert.Equal(AgentControllerType.AI, mirror.Controller);

                // The already-removed troop was not restored or adopted.
                Assert.False(registry.TryGetAgentInfo(removedId, out _));

                GC.KeepAlive(controller);
            });
        }
        finally
        {
            BattleSpawnGate.EndBattle();
        }
    }

    /// <summary>
    /// The takeover is the HOST's alone. A non-host client that receives the same <see cref="MissionPeerDisconnected"/>
    /// adopts nothing (the <c>IsLocalHost</c> gate): the dropped player's puppet keeps its assignment and its
    /// authority, and stays an inert puppet rather than being converted to AI.
    /// </summary>
    [Fact]
    [Trait("Requirement", "BR-031")]
    public void NonHostClient_IgnoresDisconnect_AdoptsNothing()
    {
        using var fixture = new MissionEngineFixture();
        var client = Clients.First();
        SetControllerId(client, "B");

        var survivorId = Guid.NewGuid();
        var mapEventId = "battle-disconnect-nonhost";

        try
        {
            client.Call(() =>
            {
                var mock = fixture.CreateMission(client);
                var controller = client.Resolve<CoopBattleController>();
                var registry = client.Resolve<INetworkAgentRegistry>();

                // "H" is the host; this client ("B") is only a successor.
                controller.Session.TryBegin(mapEventId);
                client.Resolve<IBattleHostRegistry>().Set(mapEventId, new BattleHostAssignment("H", new[] { "B" }));

                BattleSpawnGate.BeginBattle(mapEventId);

                var team = new MockTeam(BattleSideEnum.Attacker);
                BasicCharacterObject character = Game.Current.PlayerTroop;
                var survivor = mock.SpawnAgent(new AgentBuildData(character).Controller(AgentControllerType.None).Team(team.Shell));
                Assert.True(registry.TryRegisterAgent("C", survivorId, survivor));

                client.Resolve<IMessageBroker>().Publish(this, new MissionPeerDisconnected("C", mapEventId));

                // Only the host assumes control on a disconnect — a non-host adopts nothing: unchanged authority
                // and assignment, and still an inert puppet (not AI).
                Assert.True(registry.TryGetAgentInfo(survivorId, out var info));
                Assert.Equal("C", info.CurrentAuthority);
                Assert.Equal("C", info.OriginalOwner);
                Assert.True(AgentMirror.TryGet(survivor, out var mirror));
                Assert.Equal(AgentControllerType.None, mirror.Controller);

                GC.KeepAlive(controller);
            });
        }
        finally
        {
            BattleSpawnGate.EndBattle();
        }
    }
}
