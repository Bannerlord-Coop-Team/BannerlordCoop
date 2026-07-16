using System;
using System.Linq;
using E2E.Tests.Environment.MockEngine;
using Missions;
using Missions.Agents.Packets;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;
using Xunit;
using Xunit.Abstractions;
using AgentData = Missions.Agents.Packets.AgentData;

namespace E2E.Tests.Services.Missions;

/// <summary>
/// Blocking sync: a held block is native guard STATE (<see cref="Agent.CurrentGuardMode"/>), not just an
/// animation — a Controller.None puppet has nothing writing its guard, so the defend action alone blends
/// right back out and puppets never visibly (or functionally) blocked. The owner's guard now rides every
/// <see cref="AgentData"/> movement snapshot and is asserted on the puppet through the engine's guard API
/// (<see cref="Agent.SetWeaponGuard"/> / <see cref="Agent.ResetGuard"/>), only when it changes.
/// </summary>
public class BattleBlockingSyncTests : MissionTestEnvironment
{
    public BattleBlockingSyncTests(ITestOutputHelper output) : base(output) { }

    /// <summary>The owner's held block travels the movement packet and raises/lowers the puppet's guard.</summary>
    [Fact]
    public void MovementPacket_CarriesTheOwnersGuard_AndDrivesThePuppetsBlock()
    {
        using var fixture = new MissionEngineFixture();
        var peer = Clients.First();
        SetControllerId(peer, "peer");

        var agentId = Guid.NewGuid();

        peer.Call(() =>
        {
            var mock = fixture.CreateMission(peer);
            var component = peer.Resolve<ICoopMissionComponent>();
            var registry = peer.Resolve<INetworkAgentRegistry>();

            // Our local copy of another owner's troop.
            BasicCharacterObject character = Game.Current.PlayerTroop;
            var puppet = mock.SpawnAgent(new AgentBuildData(character).Controller(AgentControllerType.None));
            Assert.True(registry.TryRegisterAgent("owner", agentId, puppet));

            // The owner's copy of that troop, holding a LEFT block — the source of the snapshot.
            var remote = mock.SpawnAgent(new AgentBuildData(character).Controller(AgentControllerType.Player));
            Assert.True(AgentMirror.TryGet(remote, out var remoteMirror));
            remoteMirror.GuardMode = Agent.GuardMode.Left;

            component.AgentMovementHandler.HandlePacket(null,
                new MovementPacket(new[] { agentId }, new[] { new AgentData(remote) }));

            Assert.True(AgentMirror.TryGet(puppet, out var puppetMirror));
            Assert.Equal(Agent.GuardMode.Left, puppetMirror.GuardMode);

            // The owner lowers the block; the next snapshot resets the puppet's guard.
            remoteMirror.GuardMode = Agent.GuardMode.None;
            component.AgentMovementHandler.HandlePacket(null,
                new MovementPacket(new[] { agentId }, new[] { new AgentData(remote) }));

            Assert.Equal(Agent.GuardMode.None, puppetMirror.GuardMode);
        });
    }

    /// <summary>Every native guard direction round-trips capture → wire → apply onto the puppet.</summary>
    [Theory]
    [InlineData(Agent.GuardMode.Up)]
    [InlineData(Agent.GuardMode.Down)]
    [InlineData(Agent.GuardMode.Left)]
    [InlineData(Agent.GuardMode.Right)]
    public void GuardApply_MapsEveryDefendDirection(Agent.GuardMode held)
    {
        using var fixture = new MissionEngineFixture();
        var peer = Clients.First();

        peer.Call(() =>
        {
            var mock = fixture.CreateMission(peer);
            BasicCharacterObject character = Game.Current.PlayerTroop;
            var puppet = mock.SpawnAgent(new AgentBuildData(character).Controller(AgentControllerType.None));

            AgentData.ApplyGuardState(puppet, held);

            Assert.True(AgentMirror.TryGet(puppet, out var mirror));
            Assert.Equal(held, mirror.GuardMode);
        });
    }

    /// <summary>The apply runs per movement snapshot (~100 Hz): it must only touch the native guard when the
    /// reported state actually differs from the puppet's, never re-assert an unchanged one.</summary>
    [Fact]
    public void GuardApply_OnlyTouchesTheNativeGuard_OnChange()
    {
        using var fixture = new MissionEngineFixture();
        var peer = Clients.First();

        peer.Call(() =>
        {
            var mock = fixture.CreateMission(peer);
            BasicCharacterObject character = Game.Current.PlayerTroop;
            var puppet = mock.SpawnAgent(new AgentBuildData(character).Controller(AgentControllerType.None));
            Assert.True(AgentMirror.TryGet(puppet, out var mirror));

            // Not blocking, snapshot says not blocking: no native call.
            AgentData.ApplyGuardState(puppet, Agent.GuardMode.None);
            Assert.Equal(0, mirror.GuardWrites);

            // Raise once, then hold: one native call for the raise, none while unchanged.
            AgentData.ApplyGuardState(puppet, Agent.GuardMode.Up);
            AgentData.ApplyGuardState(puppet, Agent.GuardMode.Up);
            Assert.Equal(Agent.GuardMode.Up, mirror.GuardMode);
            Assert.Equal(1, mirror.GuardWrites);

            // Direction change and release each cost exactly one call.
            AgentData.ApplyGuardState(puppet, Agent.GuardMode.Right);
            Assert.Equal(Agent.GuardMode.Right, mirror.GuardMode);
            AgentData.ApplyGuardState(puppet, Agent.GuardMode.None);
            Assert.Equal(Agent.GuardMode.None, mirror.GuardMode);
            Assert.Equal(3, mirror.GuardWrites);
        });
    }
}
