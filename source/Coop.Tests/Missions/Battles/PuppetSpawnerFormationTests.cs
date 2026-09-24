using Common;
using Common.Tests.Utils;
using GameInterface.Services.Entity;
using GameInterface.Services.MapEvents;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Players;
using Missions;
using Missions.Battles;
using Missions.Data;
using Missions.Messages;
using Moq;
using ProtoBuf;
using System;
using System.IO;
using System.Reflection;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;
using Xunit;

namespace Coop.Tests.Missions.Battles;

/// <summary>Exercises reliable membership updates against existing and deferred remote agents without native spawning.</summary>
[Collection("Mission.Current")]
public class PuppetSpawnerFormationTests : IDisposable
{
    private readonly MissionCurrentScope mission = new();
    private readonly TestMessageBroker broker = new();
    private readonly Mock<IBattleSession> session = new();
    private readonly Mock<IAgentFormationAssigner> assigner = new();
    private readonly NetworkAgentRegistry registry = new(Mock.Of<IControllerIdProvider>());
    private readonly FormationAgentScope agentScope = new();
    private Agent agent => agentScope.Agent;
    private readonly Guid agentId = Guid.NewGuid();
    private readonly PuppetSpawner spawner;
    private int? appliedSlot;
    private int applyThread;

    public PuppetSpawnerFormationTests()
    {
        session.SetupGet(s => s.InstanceId).Returns("battle");
        session.SetupGet(s => s.OwnControllerId).Returns("local");
        session.Setup(s => s.IsOwn(It.IsAny<string>())).Returns((string owner) => owner == "local");
        var component = new Mock<ICoopMissionComponent>();
        component.SetupGet(c => c.AgentRegistry).Returns(registry);
        assigner.Setup(a => a.Assign(agent, It.IsAny<int>())).Callback<Agent, int>((_, slot) =>
        {
            appliedSlot = slot;
            applyThread = Environment.CurrentManagedThreadId;
        });
        spawner = new PuppetSpawner(broker, Mock.Of<IObjectManager>(), Mock.Of<IPlayerManager>(),
            component.Object, session.Object, Mock.Of<ICasualtyAttributionMap>(),
            Mock.Of<IBattleDeploymentCoordinator>(d => d.IsCommitted), assigner.Object,
            Mock.Of<IBattleAgentBudget>(b => b.SlotsForEquipment(It.IsAny<Equipment>()) == 1),
            Mock.Of<IMissionWeaponDataMapper>());
    }

    [Fact]
    public void PostDeploymentTransfer_UpdatesExistingRemoteAgent_OnGameThread()
    {
        Register();
        PublishSpawn(5);
        Assert.Equal(5, appliedSlot);

        broker.Publish(this, Update(7));
        Drain();

        Assert.Equal(7, appliedSlot);
        Assert.Equal(GameThread.Instance.GameThreadId, applyThread);
        Assert.True(registry.TryGetAgentInfo(agentId, out var info));
        Assert.Same(agent, info.Agent);
        Assert.Equal("remote", info.CurrentAuthority);
        Assert.Equal(0, info.AuthorityRevision);
    }

    [Fact]
    public void CatchUp_UpdatesExistingAgent_WithCustomSlotRatherThanTroopDefault()
    {
        Register();
        PublishSpawn(6);
        Assert.Equal(6, appliedSlot);
        assigner.Verify(a => a.Assign(agent), Times.Never);
    }

    [Fact]
    public void TransferWhileSpawnIsDeferred_IsNotOverwrittenByOlderBufferedRecord()
    {
        var original = Spawn(2);
        broker.Publish(this, new NetworkSpawnBattleAgents(new[] { original }));
        broker.Publish(this, Update(6));
        Drain();
        var pending = (System.Collections.Generic.List<BattleAgentSpawnData>)typeof(PuppetSpawner)
            .GetField("pendingPuppets", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(spawner)!;
        Assert.Same(original, Assert.Single(pending));
        Register();

        // Registration stands in for native spawning; drain the same buffered record through the real dedupe path.
        GameThread.Run(() => typeof(PuppetSpawner)
            .GetMethod("TrySpawnPuppetNow", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(spawner, new object[] { original, 0, false }), blocking: true);

        Assert.Equal(6, appliedSlot);
    }

    [Theory]
    [InlineData("local", 0, "local", 0, "battle")]
    [InlineData("remote", 2, "remote", 1, "battle")]
    [InlineData("remote", 2, "former", 2, "battle")]
    [InlineData("remote", 0, "remote", 0, "another-battle")]
    public void Transfer_DoesNotChangeLocalOrMismatchedAuthority(
        string owner, long revision, string sender, long messageRevision, string battle)
    {
        Register(owner, revision);
        broker.Publish(this, new NetworkBattleAgentFormationChanged(battle, agentId, sender, messageRevision, 7));
        Drain();
        Assert.Null(appliedSlot);
    }

    [Fact]
    public void WrongAuthorityUpdate_DoesNotBlockTheCurrentOwner()
    {
        Register();
        broker.Publish(this, new NetworkBattleAgentFormationChanged("battle", agentId, "former", 0, 3));
        broker.Publish(this, Update(7));
        Drain();
        Assert.Equal(7, appliedSlot);
    }

    [Fact]
    public void StaleCatchUp_DoesNotUndoCurrentAuthorityTransfer()
    {
        Register("remote", 2);
        broker.Publish(this, Update(7, revision: 2));
        PublishSpawn(3, revision: 1);
        Assert.Equal(7, appliedSlot);
    }

    [Fact]
    public void DisposedSpawner_DoesNotApplyMembership()
    {
        Register();
        spawner.Dispose();
        broker.Publish(this, Update(4));
        Drain();
        Assert.Null(appliedSlot);
    }

    [Fact]
    public void FormationMessage_RoundTripsIdentityAuthorityAndCustomSlot()
    {
        var original = Update(7, revision: 3);
        using var stream = new MemoryStream();
        Serializer.Serialize(stream, original);
        stream.Position = 0;
        var result = Serializer.Deserialize<NetworkBattleAgentFormationChanged>(stream);
        Assert.Equal(original.BattleInstanceId, result.BattleInstanceId);
        Assert.Equal(original.AgentId, result.AgentId);
        Assert.Equal(original.ControllerId, result.ControllerId);
        Assert.Equal(original.AuthorityRevision, result.AuthorityRevision);
        Assert.Equal(original.FormationIndex, result.FormationIndex);
    }

    // Use the real registry identity checks without constructing a native agent.
    private void Register(string owner = "remote", long revision = 0)
    {
        Assert.True(registry.TryRegisterAgent(owner, "remote", "remote", agentId, 1, agent, revision));
    }

    private NetworkBattleAgentFormationChanged Update(int slot, long revision = 0)
        => new("battle", agentId, "remote", revision, slot);

    private BattleAgentSpawnData Spawn(int slot, long revision = 0)
        => new(agentId, "troop", default, BattleSideEnum.Attacker, 100, "remote", null, 0,
            null, default, null, formationIndex: slot, movementId: 1, authorityRevision: revision);

    private void PublishSpawn(int slot, long revision = 0)
    {
        broker.Publish(this, new NetworkSpawnBattleAgents(new[] { Spawn(slot, revision) }));
        Drain();
    }

    private static void Drain() => GameThread.Run(() => { }, blocking: true);

    public void Dispose()
    {
        Drain();
        spawner.Dispose();
        registry.Dispose();
        broker.Dispose();
        mission.Dispose();
        agentScope.Dispose();
    }
}
