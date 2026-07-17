using System;
using System.Linq;
using E2E.Tests.Environment.Mock;
using E2E.Tests.Environment.MockEngine;
using Missions;
using Missions.Agents.Packets;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;
using Xunit;
using Xunit.Abstractions;

namespace E2E.Tests.Services.Missions;

/// <summary>
/// Verifies that reliable action updates continuously assert the owner's native guard on a Controller.None puppet.
/// </summary>
public class BattleBlockingSyncTests : MissionTestEnvironment
{
    public BattleBlockingSyncTests(ITestOutputHelper output) : base(output) { }

    [Fact]
    public void PollActions_GuardOnlyTransition_SendsActionPacket()
    {
        using var fixture = new MissionEngineFixture();
        var owner = Clients.First();
        SetControllerId(owner, "owner");

        owner.Call(() =>
        {
            var mock = fixture.CreateMission(owner);
            var component = owner.Resolve<ICoopMissionComponent>();
            var registry = owner.Resolve<INetworkAgentRegistry>();
            var network = owner.Resolve<MockBattleNetwork>();
            var agentId = Guid.NewGuid();

            var agent = mock.SpawnAgent(new AgentBuildData(Game.Current.PlayerTroop)
                .Controller(AgentControllerType.Player));
            Assert.True(registry.TryRegisterAgent("owner", agentId, agent));
            Assert.True(AgentMirror.TryGet(agent, out var mirror));

            int action0 = agent.GetCurrentAction(0).Index;
            int action1 = agent.GetCurrentAction(1).Index;
            component.AgentActionHandler.PollActions();
            Assert.Empty(network.NetworkSentPackets.GetPackets<AgentActionPacket>());

            mirror.GuardMode = Agent.GuardMode.Left;
            component.AgentActionHandler.PollActions();

            AgentActionPacket packet = Assert.Single(
                network.NetworkSentPackets.GetPackets<AgentActionPacket>());
            Assert.Equal(agentId, Assert.Single(packet.AgentIds));
            AgentActionData data = Assert.Single(packet.Actions);
            Assert.Equal(action0, data.Action0Index);
            Assert.Equal(action1, data.Action1Index);
            Assert.Equal(Agent.GuardMode.Left, data.GuardMode);
        });
    }

    [Fact]
    public void CatchUpJoiner_HeldGuard_SendsCurrentStateToJoiningPeer()
    {
        using var fixture = new MissionEngineFixture();
        var owner = Clients.First();
        SetControllerId(owner, "owner");

        owner.Call(() =>
        {
            var mock = fixture.CreateMission(owner);
            var component = owner.Resolve<ICoopMissionComponent>();
            var registry = owner.Resolve<INetworkAgentRegistry>();
            var network = owner.Resolve<MockBattleNetwork>();
            var agentId = Guid.NewGuid();

            var agent = mock.SpawnAgent(new AgentBuildData(Game.Current.PlayerTroop)
                .Controller(AgentControllerType.Player));
            Assert.True(registry.TryRegisterAgent("owner", agentId, agent));
            Assert.True(AgentMirror.TryGet(agent, out var mirror));

            mirror.GuardMode = Agent.GuardMode.Right;
            component.AgentActionHandler.PollActions();
            network.NetworkSentPackets.Packets.Clear();

            component.AgentActionHandler.CatchUpJoiner("joiner");

            var directSend = Assert.Single(network.DirectPacketSends);
            Assert.Equal("joiner", directSend.ControllerId);
            var packet = Assert.IsType<AgentActionPacket>(directSend.Packet);
            Assert.Equal(agentId, Assert.Single(packet.AgentIds));
            Assert.Equal(Agent.GuardMode.Right, Assert.Single(packet.Actions).GuardMode);
        });
    }

    [Fact]
    public void ActionTick_ReassertsHeldGuardAfterNativeDecay_WithoutAnotherPacket()
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
            BasicCharacterObject character = Game.Current.PlayerTroop;

            var puppet = mock.SpawnAgent(new AgentBuildData(character).Controller(AgentControllerType.None));
            Assert.True(registry.TryRegisterAgent("owner", agentId, puppet));

            var owner = mock.SpawnAgent(new AgentBuildData(character).Controller(AgentControllerType.Player));
            Assert.True(AgentMirror.TryGet(owner, out var ownerMirror));
            Assert.True(AgentMirror.TryGet(puppet, out var puppetMirror));

            ownerMirror.GuardMode = Agent.GuardMode.Left;
            ApplyOwnerAction(component, agentId, owner);
            component.AgentActionHandler.ApplyRemoteGuardStates();

            Assert.Equal(Agent.GuardMode.Left, puppetMirror.GuardMode);
            Assert.Equal(1, puppetMirror.SetWeaponGuardCalls);
            Assert.Equal(0, puppetMirror.ResetGuardCalls);

            puppetMirror.GuardMode = Agent.GuardMode.None;
            component.AgentActionHandler.ApplyRemoteGuardStates();

            Assert.Equal(Agent.GuardMode.Left, puppetMirror.GuardMode);
            Assert.Equal(2, puppetMirror.SetWeaponGuardCalls);

            ownerMirror.GuardMode = Agent.GuardMode.None;
            ApplyOwnerAction(component, agentId, owner);
            component.AgentActionHandler.ApplyRemoteGuardStates();
            component.AgentActionHandler.ApplyRemoteGuardStates();

            Assert.Equal(Agent.GuardMode.None, puppetMirror.GuardMode);
            Assert.Equal(1, puppetMirror.ResetGuardCalls);
        });
    }

    [Theory]
    [InlineData(Agent.GuardMode.Up)]
    [InlineData(Agent.GuardMode.Down)]
    [InlineData(Agent.GuardMode.Left)]
    [InlineData(Agent.GuardMode.Right)]
    public void GuardApply_MapsEveryGuardDirection(Agent.GuardMode guardMode)
    {
        using var fixture = new MissionEngineFixture();
        var peer = Clients.First();

        peer.Call(() =>
        {
            var mock = fixture.CreateMission(peer);
            var puppet = mock.SpawnAgent(new AgentBuildData(Game.Current.PlayerTroop)
                .Controller(AgentControllerType.None));

            AgentActionData.ApplyGuardState(puppet, guardMode);

            Assert.True(AgentMirror.TryGet(puppet, out var mirror));
            Assert.Equal(guardMode, mirror.GuardMode);
            Assert.Equal(1, mirror.SetWeaponGuardCalls);
            Assert.Equal(0, mirror.ResetGuardCalls);
        });
    }

    [Fact]
    public void GuardApply_ReassertsHeldGuard_AndSkipsRedundantReset()
    {
        using var fixture = new MissionEngineFixture();
        var peer = Clients.First();

        peer.Call(() =>
        {
            var mock = fixture.CreateMission(peer);
            var puppet = mock.SpawnAgent(new AgentBuildData(Game.Current.PlayerTroop)
                .Controller(AgentControllerType.None));
            Assert.True(AgentMirror.TryGet(puppet, out var mirror));

            AgentActionData.ApplyGuardState(puppet, Agent.GuardMode.None);
            Assert.Equal(0, mirror.ResetGuardCalls);

            AgentActionData.ApplyGuardState(puppet, Agent.GuardMode.Up);
            AgentActionData.ApplyGuardState(puppet, Agent.GuardMode.Up);
            Assert.Equal(2, mirror.SetWeaponGuardCalls);

            AgentActionData.ApplyGuardState(puppet, Agent.GuardMode.None);
            AgentActionData.ApplyGuardState(puppet, Agent.GuardMode.None);
            Assert.Equal(Agent.GuardMode.None, mirror.GuardMode);
            Assert.Equal(1, mirror.ResetGuardCalls);
        });
    }

    private static void ApplyOwnerAction(ICoopMissionComponent component, Guid agentId, Agent owner)
    {
        component.AgentActionHandler.HandlePacket(null,
            new AgentActionPacket(new[] { agentId }, new[] { new AgentActionData(owner) }));
    }
}
