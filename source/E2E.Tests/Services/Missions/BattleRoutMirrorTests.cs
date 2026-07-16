using System;
using System.Linq;
using Common.Messaging;
using E2E.Tests.Environment.MockEngine;
using Missions;
using Missions.Battles;
using Missions.Messages;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;
using Xunit;
using Xunit.Abstractions;

namespace E2E.Tests.Services.Missions;

/// <summary>Regression coverage for peer-side battle rout application.</summary>
public class BattleRoutMirrorTests : MissionTestEnvironment
{
    public BattleRoutMirrorTests(ITestOutputHelper output) : base(output) { }

    [Fact]
    public void RoutBeforeRegistration_AppliesWhenPendingRoutsDrain()
    {
        using var fixture = new MissionEngineFixture();
        var peer = Clients.First();
        SetControllerId(peer, "peer");

        var agentId = Guid.NewGuid();
        Agent peerAgent = null!;

        peer.Call(() =>
        {
            var mock = fixture.CreateMission(peer);
            var registry = peer.Resolve<INetworkAgentRegistry>();
            var broker = peer.Resolve<IMessageBroker>();
            using var applier = new PuppetRoutApplier(
                broker,
                peer.Resolve<ICoopMissionComponent>(),
                new CasualtyAttributionMap());

            broker.Publish(this, new NetworkBattleAgentRouted(agentId));
            applier.DrainPendingRouts();

            peerAgent = mock.SpawnAgent(
                new AgentBuildData(Game.Current.PlayerTroop).Controller(AgentControllerType.None));
            Assert.True(registry.TryRegisterAgent("owner", agentId, peerAgent));

            applier.DrainPendingRouts();
            Assert.False(registry.TryGetAgentInfo(agentId, out _));
        });

        Assert.True(AgentMirror.TryGet(peerAgent, out var agentMirror));
        Assert.False(agentMirror.IsActive);
        Assert.False(agentMirror.WasKilled);
    }
}
