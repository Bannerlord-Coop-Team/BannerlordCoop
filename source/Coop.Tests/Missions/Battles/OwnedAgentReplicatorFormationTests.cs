using Common.Messaging;
using Common.Tests.Utils;
using GameInterface.Services.Entity;
using GameInterface.Services.MapEvents.Messages;
using GameInterface.Services.ObjectManager;
using Missions;
using Missions.Battles;
using Missions.Data;
using Missions.Messages;
using Moq;
using System;
using System.Collections.Generic;
using System.Reflection;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;
using Xunit;

namespace Coop.Tests.Missions.Battles;

/// <summary>Checks owner-only membership publication and the reliable spawn-before-transfer barrier.</summary>
[Collection("Mission.Current")]
public class OwnedAgentReplicatorFormationTests
{
    [Theory]
    [InlineData("local", false, true)]
    [InlineData("remote", false, false)]
    [InlineData("local", true, false)]
    public void FormationChange_OnlyBroadcastsRevealedLocallyOwnedAgents(string owner, bool withheld, bool expected)
    {
        using var broker = new TestMessageBroker();
        using var registry = new NetworkAgentRegistry(Mock.Of<IControllerIdProvider>());
        using var agentScope = new FormationAgentScope();
        var agent = agentScope.Agent;
        typeof(Agent).GetField("_character", BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(agent, new CharacterObject());
        var agentId = Guid.NewGuid();
        Assert.True(registry.TryRegisterAgent(owner, agentId, 1, agent, authorityRevision: 4));
        var component = new Mock<ICoopMissionComponent>();
        component.SetupGet(c => c.AgentRegistry).Returns(registry);
        var session = new Mock<IBattleSession>();
        session.SetupGet(s => s.OwnControllerId).Returns("local");
        session.SetupGet(s => s.InstanceId).Returns("battle");
        var deployment = new Mock<IBattleDeploymentCoordinator>();
        deployment.Setup(d => d.ShouldWithhold(It.IsAny<bool>())).Returns(withheld);
        var network = new Mock<IBattleNetwork>();
        var sent = new List<IMessage>();
        network.Setup(n => n.SendAll(It.IsAny<IMessage>())).Callback<IMessage>(sent.Add);
        var codec = new Mock<IBattleAgentSpawnBatchCodec>();
        var spawnBatch = new NetworkSpawnBattleAgents(Array.Empty<BattleAgentSpawnData>());
        codec.Setup(c => c.Encode(It.IsAny<IReadOnlyList<BattleAgentSpawnData>>(), SpawnBatchPurpose.Initial))
            .Returns(new[] { spawnBatch });
        using var replicator = new OwnedAgentReplicator(network.Object, broker, Mock.Of<IObjectManager>(),
            component.Object, session.Object, Mock.Of<ICasualtyAttributionMap>(), deployment.Object,
            codec.Object, Mock.Of<IMissionWeaponDataMapper>());
        var pending = (List<BattleAgentSpawnData>)typeof(OwnedAgentReplicator)
            .GetField("pendingSpawns", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(replicator)!;
        pending.Add(new BattleAgentSpawnData(agentId, "troop", default, BattleSideEnum.Attacker, 100,
            owner, null, 0, null, default, null, formationIndex: 2));

        broker.Publish(this, new BattleAgentFormationChanged(agent, 7));

        if (!expected)
        {
            Assert.Empty(sent);
            Assert.Single(pending);
            return;
        }

        Assert.Equal(2, sent.Count);
        Assert.Same(spawnBatch, sent[0]);
        var transfer = Assert.IsType<NetworkBattleAgentFormationChanged>(sent[1]);
        Assert.Equal(agentId, transfer.AgentId);
        Assert.Equal("battle", transfer.BattleInstanceId);
        Assert.Equal(owner, transfer.ControllerId);
        Assert.Equal(4, transfer.AuthorityRevision);
        Assert.Equal(7, transfer.FormationIndex);
        Assert.Empty(pending);
    }
}
