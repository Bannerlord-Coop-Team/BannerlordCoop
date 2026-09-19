using Common.Messaging;
using Common;
using GameInterface.Services.MapEvents;
using HarmonyLib;
using Missions;
using Missions.Battles;
using Missions.Messages;
using Moq;
using ProtoBuf;
using System;
using System.Collections.Generic;
using System.Runtime.Serialization;
using TaleWorlds.Core;
using TaleWorlds.Engine;
using TaleWorlds.MountAndBlade;
using Xunit;

namespace Coop.Tests.Missions.Battles;

[Collection("Mission.Current")]
public sealed class MangonelLoadReplicationTests : IDisposable
{
    private readonly MissionCurrentScope mission = new();
    private readonly Harmony harmony = new("coop.tests.mangonel-load." + Guid.NewGuid());
    private readonly List<SiegeMachineStateReplicator> replicas = new();
    private readonly Guid agentId = Guid.NewGuid();
    private readonly Guid grantId = Guid.NewGuid();
    private readonly ItemObject missile = new("boulder");
    private static readonly Dictionary<Agent, int> Actions = new();
    private static readonly Dictionary<Agent, int> Removals = new();
    private static readonly Dictionary<UsableMissionObject, Agent> Users = new();
    private static readonly Dictionary<Agent, ActionIndexCache> CurrentActions = new();
    private static bool changeActionOnUse;

    public MangonelLoadReplicationTests()
    {
        missile.AddWeapon(new WeaponComponentData(null, WeaponClass.Boulder, default), null);
        Stub(typeof(ScriptComponentBehavior), "CacheEditableFieldsForAllScriptComponents", nameof(Skip));
        Stub(typeof(Agent), nameof(Agent.IsActive), nameof(Active));
        Stub(typeof(Agent), nameof(Agent.GetPrimaryWieldedItemIndex), nameof(MainHand));
        Stub(typeof(Agent), nameof(Agent.GetOffhandWieldedItemIndex), nameof(OffHand));
        Stub(typeof(Agent), nameof(Agent.UseGameObject), nameof(Use));
        Stub(typeof(Agent), nameof(Agent.StopUsingGameObject), nameof(Stop));
        Stub(typeof(Agent), nameof(Agent.SetActionChannel), nameof(Action));
        Stub(typeof(Agent), nameof(Agent.GetCurrentAction), nameof(CurrentAction));
        Stub(typeof(Agent), nameof(Agent.RemoveEquippedWeapon), nameof(Remove));
        Stub(typeof(MBAnimation), nameof(MBAnimation.GetActionCodeWithName), nameof(ActionCode));
        harmony.Patch(AccessTools.PropertyGetter(typeof(Agent), nameof(Agent.IsHuman)),
            prefix: new HarmonyMethod(typeof(MangonelLoadReplicationTests), nameof(Active)));
        harmony.Patch(AccessTools.PropertyGetter(typeof(UsableMissionObject), nameof(UsableMissionObject.UserAgent)),
            prefix: new HarmonyMethod(typeof(MangonelLoadReplicationTests), nameof(User)));
        harmony.Patch(AccessTools.PropertyGetter(typeof(UsableMissionObject), nameof(UsableMissionObject.HasUser)),
            prefix: new HarmonyMethod(typeof(MangonelLoadReplicationTests), nameof(HasUser)));
    }

    private (SiegeMachineStateReplicator Sut, CoopAgentInfo Info, Mangonel Machine, Mock<IBattleNetwork> Network) Replica(string own)
    {
        var agent = New<Agent>();
        AccessTools.Property(typeof(Agent), "Mission").SetValue(agent, mission.Instance);
        AccessTools.Property(typeof(Agent), "Equipment").SetValue(agent, new MissionEquipment());
        agent.Equipment[EquipmentIndex.ExtraWeaponSlot] = new MissionWeapon(missile, null, null, 1);
        var info = new CoopAgentInfo("loader", "loader", "battle", agent, agentId, 1, 7);
        info.RecordSiegeGrant(grantId);
        var point = New<StandingPointWithWeaponRequirement>();
        var machine = New<Mangonel>();
        AccessTools.Property(typeof(MissionObject), "Id").SetValue(machine, new MissionObjectId(1496, false));
        AccessTools.Property(typeof(MissionObject), "Id").SetValue(point, new MissionObjectId(1487, false));
        AccessTools.Field(typeof(RangedSiegeWeapon), "LoadAmmoStandingPoint").SetValue(machine, point);
        AccessTools.Field(typeof(RangedSiegeWeapon), "OriginalMissileItem").SetValue(machine, missile);
        AccessTools.Field(typeof(Mangonel), "_loadAmmoEndAnimationActionIndex").SetValue(machine, ActionIndex(12));
        AccessTools.Field(typeof(Mangonel), "_loadAmmoBeginAnimationActionIndex").SetValue(machine, ActionIndex(11));
        SetState(machine, own == "simulator" ? RangedSiegeWeapon.WeaponState.LoadingAmmo : RangedSiegeWeapon.WeaponState.Idle);
        var registry = new Mock<INetworkAgentRegistry>();
        registry.Setup(r => r.TryGetAgentInfo(agentId, out info)).Returns(true);
        registry.Setup(r => r.TryGetAgentInfo(agent, out info)).Returns(true);
        registry.Setup(r => r.GetAgents(own)).Returns(own == "loader" ? new[] { info } : Array.Empty<CoopAgentInfo>());
        var session = new Mock<IBattleSession>();
        session.SetupGet(s => s.InstanceId).Returns("battle");
        session.SetupGet(s => s.OwnControllerId).Returns(own);
        session.SetupGet(s => s.HostEpoch).Returns(3);
        var network = new Mock<IBattleNetwork>();
        var sut = new SiegeMachineStateReplicator(network.Object, Mock.Of<IMessageBroker>(), session.Object,
            registry.Object, new HostEpochPolicy());
        Field<Dictionary<int, UsableMachine>>(sut, "machinesById")[1496] = machine;
        Field<List<UsableMachine>>(sut, "machines").Add(machine);
        Field<Dictionary<int, string>>(sut, "claimedMachines")[1496] = "simulator";
        Field<Dictionary<int, int>>(sut, "authorityEpochs")[1496] = 3;
        Field<Dictionary<int, int>>(sut, "authorityRevisions")[1496] = 2;
        replicas.Add(sut);
        return (sut, info, machine, network);
    }

#if DEBUG
    private static Agent diagnosticMover;
    private static bool diagnosticContest;

    private static bool DiagnosticMovingAgent(ref Agent __result) { __result = diagnosticMover; return false; }
    private static bool DiagnosticContested(ref bool __result) { __result = diagnosticContest; return false; }

    [Theory]
    [InlineData(false, false, false)]
    [InlineData(true, false, false)]
    [InlineData(false, true, false)]
    [InlineData(false, false, true)]
    public void AuthorityObservation_RetainsBlockersAndTimersWithoutChangingAuthority(bool user, bool mover, bool contested)
    {
        var replica = Replica("loader");
        AccessTools.Property(typeof(UsableMachine), nameof(UsableMachine.StandingPoints)).SetValue(replica.Machine,
            new TaleWorlds.Library.MBList<StandingPoint> { Point(replica.Machine) });
        AccessTools.Field(typeof(SiegeMachineStateReplicator), "trackedMission").SetValue(replica.Sut, mission.Instance);
        var session = Mock.Get(Field<IBattleSession>(replica.Sut, "session"));
        session.SetupGet(s => s.HostControllerId).Returns("host");
        session.SetupGet(s => s.IsLocalHost).Returns(true);
        // A stale stored epoch must remain untouched even on the host's read path.
        Field<Dictionary<int, int>>(replica.Sut, "authorityEpochs")[1496] = 2;
        Field<Dictionary<int, float>>(replica.Sut, "unusedOwnedSeconds")[1496] = 1.25f;
        Field<Dictionary<int, float>>(replica.Sut, "grantGrace")[1496] = 4f;
        Mock.Get(Field<INetworkAgentRegistry>(replica.Sut, "agentRegistry"))
            .Setup(r => r.IsLocallyControlled(replica.Info.Agent)).Returns(true);
        if (user) Use(replica.Info.Agent, Point(replica.Machine));
        diagnosticMover = mover ? replica.Info.Agent : null;
        diagnosticContest = contested;
        harmony.Patch(AccessTools.PropertyGetter(typeof(UsableMissionObject), nameof(UsableMissionObject.MovingAgent)),
            prefix: new HarmonyMethod(typeof(MangonelLoadReplicationTests), nameof(DiagnosticMovingAgent)));
        Stub(typeof(UsableMachine), nameof(UsableMachine.IsDisabledForBattleSideAI), nameof(DiagnosticContested));

        var value = Newtonsoft.Json.Linq.JObject.FromObject(replica.Sut.ObserveMachineAuthority(replica.Machine));
        Assert.True((bool)value["available"]);
        Assert.Equal("simulator", (string)value["simulator"]);
        Assert.Equal(2, (int)value["authorityEpoch"]);
        Assert.Equal(3, (int)value["hostEpoch"]);
        Assert.Equal(user, (bool)value["localUser"]);
        Assert.Equal(mover, (bool)value["localMover"]);
        Assert.Equal(contested, (bool)value["contested"]);
        Assert.Equal(1.25f, (float)value["unusedSeconds"]);
        Assert.Equal(4f, (float)value["graceSeconds"]);
        Assert.Equal(2, Field<Dictionary<int, int>>(replica.Sut, "authorityEpochs")[1496]);
        Assert.Equal("simulator", Field<Dictionary<int, string>>(replica.Sut, "claimedMachines")[1496]);
        replica.Network.Verify(n => n.SendAll(It.IsAny<IMessage>()), Times.Never);
    }

    [Fact]
    public void AuthorityObservation_RejectsStaleMissionAndSameIdReplacement()
    {
        var replica = Replica("loader");
        Assert.False((bool)Newtonsoft.Json.Linq.JObject.FromObject(replica.Sut.ObserveMachineAuthority(replica.Machine))["available"]);
        AccessTools.Field(typeof(SiegeMachineStateReplicator), "trackedMission").SetValue(replica.Sut, mission.Instance);
        Field<Dictionary<int, UsableMachine>>(replica.Sut, "machinesById")[1496] = New<Mangonel>();
        Assert.False((bool)Newtonsoft.Json.Linq.JObject.FromObject(replica.Sut.ObserveMachineAuthority(replica.Machine))["available"]);
    }
#endif

    [Fact]
    public void CrossControllerLoad_UsesOwnerAnimationAndConsumesOnlyAfterNativeCompletion()
    {
        var owner = Replica("loader");
        var simulator = Replica("simulator");
        Use(owner.Info.Agent, Point(owner.Machine));
        Tick(owner.Sut);
        var request = Sent(owner.Network, MangonelLoadPhase.Request);
        var wire = Serializer.DeepClone(request);
        Assert.True(request.Matches(wire));
        simulator.Sut.ApplyMangonelLoad(wire);
        Assert.Null(simulator.Info.Agent.CurrentlyUsedGameObject);
        Assert.False(Actions.ContainsKey(simulator.Info.Agent));
        owner.Sut.ApplyMangonelLoad(Sent(simulator.Network, MangonelLoadPhase.Animate));
        Assert.Equal(1, Actions[owner.Info.Agent]);
        Assert.Equal(RangedSiegeWeapon.WeaponState.Idle, owner.Machine.State);
        Assert.Equal(1, owner.Info.Agent.Equipment[EquipmentIndex.ExtraWeaponSlot].Amount);
        simulator.Network.Verify(n => n.SendAll(It.Is<NetworkMangonelLoad>(m => m.Phase == MangonelLoadPhase.Consumed)), Times.Never);

        NativeLoadTick(simulator.Sut, simulator.Machine);
        Assert.Null(simulator.Info.Agent.CurrentlyUsedGameObject);
        CurrentActions[simulator.Info.Agent] = ActionIndex(12);
        NativeLoadTick(simulator.Sut, simulator.Machine);
        Assert.Null(simulator.Info.Agent.CurrentlyUsedGameObject);
        Tick(owner.Sut);
        owner.Network.Verify(n => n.SendAll(It.Is<NetworkMangonelLoad>(m => m.Phase == MangonelLoadPhase.Ready)), Times.Never);
        CurrentActions[owner.Info.Agent] = ActionIndex(12);
        Tick(owner.Sut);
        Tick(owner.Sut);
        simulator.Sut.ApplyMangonelLoad(Sent(owner.Network, MangonelLoadPhase.Ready));
        NativeLoadTick(simulator.Sut, simulator.Machine);
        Assert.Same(Point(simulator.Machine), simulator.Info.Agent.CurrentlyUsedGameObject);
        Assert.False(Actions.ContainsKey(simulator.Info.Agent));
        Assert.False(Removals.ContainsKey(simulator.Info.Agent));

        Remove(simulator.Info.Agent, EquipmentIndex.ExtraWeaponSlot);
        SetState(simulator.Machine, RangedSiegeWeapon.WeaponState.WaitingBeforeIdle);
        Complete(simulator.Sut, simulator.Machine, simulator.Info.Agent);
        var completion = Sent(simulator.Network, MangonelLoadPhase.Consumed);
        owner.Sut.ApplyMangonelLoad(completion);
        owner.Sut.ApplyMangonelLoad(completion);
        owner.Sut.ApplyMangonelLoad(Sent(simulator.Network, MangonelLoadPhase.Animate));
        Assert.True(owner.Info.Agent.Equipment[EquipmentIndex.ExtraWeaponSlot].IsEmpty);
        Assert.Null(owner.Info.Agent.CurrentlyUsedGameObject);
        Assert.Equal(1, Removals[owner.Info.Agent]);
        Assert.Equal(2, Actions[owner.Info.Agent]); // Load action, then release; duplicate start is ignored.
        Assert.True(owner.Info.IsSiegeGrantConsumed(grantId));
    }

    [Theory]
    [InlineData("other-battle", "loader", 7, 3, 2, 1487, "loader")]
    [InlineData("battle", "foreign", 7, 3, 2, 1487, "foreign")]
    [InlineData("battle", "loader", 6, 3, 2, 1487, "loader")]
    [InlineData("battle", "loader", 7, 2, 2, 1487, "loader")]
    [InlineData("battle", "loader", 7, 3, 1, 1487, "loader")]
    [InlineData("battle", "loader", 7, 3, 2, 1486, "loader")]
    [InlineData("battle", "loader", 7, 3, 2, 1487, "foreign")]
    public void Request_RejectsStaleIdentityBeforeUse(string battle, string authority, long revision,
        int epoch, int machineRevision, int pointId, string sender)
    {
        var simulator = Replica("simulator");
        simulator.Sut.ApplyMangonelLoad(new NetworkMangonelLoad(battle, Guid.NewGuid(), grantId, agentId,
            authority, revision, 1496, pointId, "simulator", epoch, machineRevision, MangonelLoadPhase.Request, sender));
        Assert.Null(simulator.Info.Agent.CurrentlyUsedGameObject);
        simulator.Network.Verify(n => n.Send(It.IsAny<string>(), It.IsAny<IMessage>()), Times.Never);
    }

    [Fact]
    public void Migration_CancelsOldUseKeepsAmmoAndAcceptsFreshTransaction()
    {
        var owner = Replica("loader");
        var simulator = Replica("simulator");
        Use(owner.Info.Agent, Point(owner.Machine));
        Tick(owner.Sut);
        var old = Sent(owner.Network, MangonelLoadPhase.Request);
        simulator.Sut.ApplyMangonelLoad(old);
        owner.Sut.ApplyMangonelLoad(Sent(simulator.Network, MangonelLoadPhase.Animate));
        BeginAndEndLoad(simulator.Sut, simulator.Machine, simulator.Info.Agent);
        foreach (var sut in new[] { owner.Sut, simulator.Sut })
        {
            Field<Dictionary<int, int>>(sut, "authorityRevisions")[1496] = 4;
            Tick(sut);
        }
        Assert.Null(owner.Info.Agent.CurrentlyUsedGameObject);
        Assert.Null(simulator.Info.Agent.CurrentlyUsedGameObject);
        Assert.Equal(1, owner.Info.Agent.Equipment[EquipmentIndex.ExtraWeaponSlot].Amount);
        Complete(simulator.Sut, simulator.Machine, simulator.Info.Agent);
        simulator.Network.Verify(n => n.SendAll(It.Is<NetworkMangonelLoad>(m => m.Phase == MangonelLoadPhase.Consumed)), Times.Never);
        Assert.Equal(1, owner.Info.Agent.Equipment[EquipmentIndex.ExtraWeaponSlot].Amount);
        owner.Network.Invocations.Clear();
        Use(owner.Info.Agent, Point(owner.Machine));
        Tick(owner.Sut);
        var fresh = Sent(owner.Network, MangonelLoadPhase.Request);
        Assert.NotEqual(old.RequestId, fresh.RequestId);
        Assert.Equal(4, fresh.MachineRevision);
        simulator.Sut.ApplyMangonelLoad(fresh);
        NativeLoadTick(simulator.Sut, simulator.Machine);
        Assert.Null(simulator.Info.Agent.CurrentlyUsedGameObject);
        Assert.Equal(1, simulator.Info.Agent.Equipment[EquipmentIndex.ExtraWeaponSlot].Amount);
        BeginAndEndLoad(simulator.Sut, simulator.Machine, simulator.Info.Agent);
        Assert.Same(Point(simulator.Machine), simulator.Info.Agent.CurrentlyUsedGameObject);
    }

    [Fact]
    public void CompletionBeforeGrant_TombstonesOldGrantWithoutRemovingNewerAmmo()
    {
        var peer = Replica("peer");
        Guid newer = Guid.NewGuid();
        peer.Info.RecordSiegeGrant(newer);
        peer.Sut.ApplyMangonelLoad(new NetworkMangonelLoad("battle", Guid.NewGuid(), grantId, agentId,
            "loader", 7, 1496, 1487, "simulator", 3, 2, MangonelLoadPhase.Consumed, "simulator"));
        Assert.True(peer.Info.IsSiegeGrantConsumed(grantId));
        Assert.Equal(newer, peer.Info.SiegeEquipmentGrant);
        Assert.Equal(1, peer.Info.Agent.Equipment[EquipmentIndex.ExtraWeaponSlot].Amount);
        Assert.False(Removals.ContainsKey(peer.Info.Agent));
    }

    [Fact]
    public void FreshRequestBeforeStaleCleanup_ReplacesOnlyTheOldAuthorityTransaction()
    {
        var simulator = Replica("simulator");
        var old = new NetworkMangonelLoad("battle", Guid.NewGuid(), grantId, agentId,
            "loader", 7, 1496, 1487, "simulator", 3, 2, MangonelLoadPhase.Request, "loader");
        simulator.Sut.ApplyMangonelLoad(old);
        Field<Dictionary<int, int>>(simulator.Sut, "authorityRevisions")[1496] = 4;
        var fresh = new NetworkMangonelLoad("battle", Guid.NewGuid(), grantId, agentId,
            "loader", 7, 1496, 1487, "simulator", 3, 4, MangonelLoadPhase.Request, "loader");
        simulator.Network.Invocations.Clear();
        simulator.Sut.ApplyMangonelLoad(fresh);
        Assert.Equal(fresh.RequestId, Sent(simulator.Network, MangonelLoadPhase.Animate).RequestId);
        Assert.Contains(old.RequestId, Field<HashSet<Guid>>(simulator.Sut, "finishedMangonelLoads"));
        simulator.Sut.ApplyMangonelLoad(old.WithPhase(MangonelLoadPhase.Ready, "loader"));
        Assert.Equal(fresh.RequestId,
            Assert.Single(Field<Dictionary<Guid, NetworkMangonelLoad>>(simulator.Sut, "mangonelLoads").Values).RequestId);
    }

    [Fact]
    public void OwnerLoadEndBetweenMachinePolls_SendsReadyOnceOnTheNextFrame()
    {
        var owner = Replica("loader");
        Use(owner.Info.Agent, Point(owner.Machine));
        Tick(owner.Sut);
        var request = Sent(owner.Network, MangonelLoadPhase.Request);
        owner.Sut.ApplyMangonelLoad(request.WithPhase(MangonelLoadPhase.Animate, "simulator"));
        harmony.Patch(AccessTools.PropertyGetter(typeof(Mission), nameof(Mission.IsSiegeBattle)),
            prefix: new HarmonyMethod(typeof(MangonelLoadReplicationTests), nameof(Active)));
        CurrentActions[owner.Info.Agent] = ActionIndex(12);
        owner.Sut.Tick(0.01f);
        Assert.Equal(request.RequestId, Sent(owner.Network, MangonelLoadPhase.Ready).RequestId);
        Assert.True(Field<float>(owner.Sut, "pollTimer") < 0.25f);
        CurrentActions[owner.Info.Agent] = default;
        owner.Sut.Tick(0.01f);
        Assert.Equal(request.RequestId, Sent(owner.Network, MangonelLoadPhase.Ready).RequestId);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void CompetingLoader_CancelsTheWaitingOwnerWithoutConsumingItsGrant(bool alreadyLoaded)
    {
        var owner = Replica("loader");
        var simulator = Replica("simulator");
        Use(owner.Info.Agent, Point(owner.Machine));
        Tick(owner.Sut);
        var request = Sent(owner.Network, MangonelLoadPhase.Request);
        simulator.Sut.ApplyMangonelLoad(request);
        owner.Sut.ApplyMangonelLoad(Sent(simulator.Network, MangonelLoadPhase.Animate));
        Agent competing = New<Agent>();
        if (alreadyLoaded) SetState(simulator.Machine, RangedSiegeWeapon.WeaponState.WaitingBeforeIdle);
        else Use(competing, Point(simulator.Machine));
        Tick(simulator.Sut);
        owner.Sut.ApplyMangonelLoad(Sent(simulator.Network, MangonelLoadPhase.Cancel));
        Assert.Null(owner.Info.Agent.CurrentlyUsedGameObject);
        Assert.Equal(1, owner.Info.Agent.Equipment[EquipmentIndex.ExtraWeaponSlot].Amount);
        Assert.False(owner.Info.IsSiegeGrantConsumed(grantId));
        Assert.False(Removals.ContainsKey(owner.Info.Agent));
        if (!alreadyLoaded) Assert.Same(competing, Point(simulator.Machine).UserAgent);
        Assert.Empty(Field<Dictionary<Guid, NetworkMangonelLoad>>(simulator.Sut, "mangonelLoads"));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void NativeCompletionBeforeMigration_ReconcilesTheGrantAndJoinerAfterNewAuthority(bool newerGrant)
    {
        var owner = Replica("loader");
        var simulator = Replica("simulator");
        var joiner = Replica("joiner"); // Its earlier spawn snapshot still carries the missile.
        Use(owner.Info.Agent, Point(owner.Machine));
        Tick(owner.Sut);
        var request = Sent(owner.Network, MangonelLoadPhase.Request);
        simulator.Sut.ApplyMangonelLoad(request);
        owner.Sut.ApplyMangonelLoad(Sent(simulator.Network, MangonelLoadPhase.Animate));
        BeginAndEndLoad(simulator.Sut, simulator.Machine, simulator.Info.Agent);
        Remove(simulator.Info.Agent, EquipmentIndex.ExtraWeaponSlot);
        SetState(simulator.Machine, RangedSiegeWeapon.WeaponState.WaitingBeforeIdle);
        Complete(simulator.Sut, simulator.Machine, simulator.Info.Agent);
        var completion = Sent(simulator.Network, MangonelLoadPhase.Consumed);
        foreach (var sut in new[] { owner.Sut, simulator.Sut, joiner.Sut })
        {
            Field<Dictionary<int, int>>(sut, "authorityRevisions")[1496] = 4;
            Tick(sut);
        }
        Guid currentGrant = newerGrant ? Guid.NewGuid() : grantId;
        owner.Info.RecordSiegeGrant(currentGrant);
        joiner.Info.RecordSiegeGrant(currentGrant);
        Use(owner.Info.Agent, Point(owner.Machine));
        Tick(owner.Sut);
        Guid freshRequest = Assert.Single(Field<Dictionary<Guid, NetworkMangonelLoad>>(
            owner.Sut, "mangonelLoads").Values).RequestId;
        owner.Sut.ApplyMangonelLoad(completion);
        owner.Sut.ApplyMangonelLoad(completion);
        Assert.Same(Point(owner.Machine), owner.Info.Agent.CurrentlyUsedGameObject);
        Assert.Equal(freshRequest, Assert.Single(Field<Dictionary<Guid, NetworkMangonelLoad>>(
            owner.Sut, "mangonelLoads").Values).RequestId);
        var terminal = Sent(owner.Network, MangonelLoadPhase.OwnerConsumed);
        joiner.Sut.ApplyMangonelLoad(completion); // No accepted historical request on the joiner.
        Assert.False(joiner.Info.Agent.Equipment[EquipmentIndex.ExtraWeaponSlot].IsEmpty);
        owner.Network.Invocations.Clear();
        AccessTools.Method(typeof(SiegeMachineStateReplicator), "ReplayMangonelLoads")
            .Invoke(owner.Sut, new object[] { "joiner" });
        Assert.Equal(terminal.RequestId, Sent(owner.Network, MangonelLoadPhase.OwnerConsumed).RequestId);
        Field<Dictionary<int, UsableMachine>>(joiner.Sut, "machinesById").Clear();
        joiner.Sut.ApplyMangonelLoad(terminal);
        Assert.False(joiner.Info.Agent.Equipment[EquipmentIndex.ExtraWeaponSlot].IsEmpty);
        Field<Dictionary<int, UsableMachine>>(joiner.Sut, "machinesById")[1496] = joiner.Machine;
        Tick(joiner.Sut);
        joiner.Sut.ApplyMangonelLoad(terminal);
        Assert.Equal(!newerGrant, owner.Info.Agent.Equipment[EquipmentIndex.ExtraWeaponSlot].IsEmpty);
        Assert.Equal(!newerGrant, joiner.Info.Agent.Equipment[EquipmentIndex.ExtraWeaponSlot].IsEmpty);
        Assert.True(owner.Info.IsSiegeGrantConsumed(grantId));
        Assert.True(joiner.Info.IsSiegeGrantConsumed(grantId));
        Assert.Null(joiner.Info.Agent.CurrentlyUsedGameObject);
        Assert.False(Actions.ContainsKey(joiner.Info.Agent));
    }

    [Theory]
    [InlineData("foreign", 7, "loader")]
    [InlineData("loader", 6, "loader")]
    [InlineData("loader", 7, "simulator")]
    public void OwnerTerminal_RejectsForeignOrStaleAgentAuthority(string owner, long revision, string sender)
    {
        var peer = Replica("peer");
        peer.Sut.ApplyMangonelLoad(new NetworkMangonelLoad("battle", Guid.NewGuid(), grantId, agentId,
            owner, revision, 1496, 1487, "simulator", 3, 2, MangonelLoadPhase.OwnerConsumed, sender));
        Assert.False(peer.Info.IsSiegeGrantConsumed(grantId));
        Assert.Equal(1, peer.Info.Agent.Equipment[EquipmentIndex.ExtraWeaponSlot].Amount);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void NativeObservation_RequiresLoadedStateAndActualEmptySlot(bool loaded)
    {
        var simulator = Replica("simulator");
        Use(simulator.Info.Agent, Point(simulator.Machine));
        bool oldAuthority = SiegeMissionAuthorityGate.IsLocalAuthority;
        SiegeMissionAuthorityGate.IsLocalAuthority = true;
        int completions = 0;
        Action<MessagePayload<MangonelAmmoConsumed>> receive = _ => completions++;
        MessageBroker.Instance.Subscribe(receive);
        try
        {
            object[] args = { simulator.Machine, null };
            AccessTools.Method(typeof(MangonelAmmoConsumedPatch), "Prefix").Invoke(null, args);
            Assert.Same(simulator.Info.Agent, args[1]);
            SetState(simulator.Machine, RangedSiegeWeapon.WeaponState.WaitingBeforeIdle);
            if (loaded) Remove(simulator.Info.Agent, EquipmentIndex.ExtraWeaponSlot);
            AccessTools.Method(typeof(MangonelAmmoConsumedPatch), "Postfix").Invoke(null, args);
            Assert.Equal(loaded ? 1 : 0, completions);
        }
        finally
        {
            MessageBroker.Instance.Unsubscribe(receive);
            SiegeMissionAuthorityGate.IsLocalAuthority = oldAuthority;
        }
    }

    [Fact]
    public void CompletionBeforeMachineRegistration_DrainsAfterCatchUpWithoutResurrection()
    {
        var peer = Replica("peer");
        Field<Dictionary<int, UsableMachine>>(peer.Sut, "machinesById").Clear();
        var complete = new NetworkMangonelLoad("battle", Guid.NewGuid(), grantId, agentId,
            "loader", 7, 1496, 1487, "simulator", 3, 2, MangonelLoadPhase.Consumed, "simulator");
        peer.Sut.ApplyMangonelLoad(complete);
        Assert.Equal(1, peer.Info.Agent.Equipment[EquipmentIndex.ExtraWeaponSlot].Amount);
        Field<Dictionary<int, UsableMachine>>(peer.Sut, "machinesById")[1496] = peer.Machine;
        Tick(peer.Sut);
        Assert.True(peer.Info.Agent.Equipment[EquipmentIndex.ExtraWeaponSlot].IsEmpty);
        peer.Sut.ApplyMangonelLoad(complete.WithPhase(MangonelLoadPhase.Request, "loader"));
        Assert.Null(peer.Info.Agent.CurrentlyUsedGameObject);
        Assert.Equal(1, Removals[peer.Info.Agent]);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void NativeCompletionAlreadySent_WinsOverOwnersLaterCancel(bool completionFirst)
    {
        var owner = Replica("loader");
        Use(owner.Info.Agent, Point(owner.Machine));
        Tick(owner.Sut);
        var request = Sent(owner.Network, MangonelLoadPhase.Request);
        Stop(owner.Info.Agent);
        if (completionFirst)
            owner.Sut.ApplyMangonelLoad(request.WithPhase(MangonelLoadPhase.Consumed, "simulator"));
        Tick(owner.Sut);
        owner.Sut.ApplyMangonelLoad(request.WithPhase(MangonelLoadPhase.Cancel, "loader"));
        Assert.Contains(request.RequestId, Field<HashSet<Guid>>(owner.Sut, "finishedMangonelLoads"));
        owner.Sut.ApplyMangonelLoad(request.WithPhase(MangonelLoadPhase.Consumed, "simulator"));
        Assert.True(owner.Info.Agent.Equipment[EquipmentIndex.ExtraWeaponSlot].IsEmpty);
    }

    [Fact]
    public void RequestBeforeMachineAuthority_WaitsForTheMatchingAssignment()
    {
        var simulator = Replica("simulator");
        var request = new NetworkMangonelLoad("battle", Guid.NewGuid(), grantId, agentId,
            "loader", 7, 1496, 1487, "simulator", 3, 4, MangonelLoadPhase.Request, "loader");
        simulator.Sut.ApplyMangonelLoad(request);
        Assert.Null(simulator.Info.Agent.CurrentlyUsedGameObject);
        Field<Dictionary<int, int>>(simulator.Sut, "authorityRevisions")[1496] = 4;
        Tick(simulator.Sut);
        Assert.Null(simulator.Info.Agent.CurrentlyUsedGameObject);
        Assert.Equal(request.RequestId, Sent(simulator.Network, MangonelLoadPhase.Animate).RequestId);
        BeginAndEndLoad(simulator.Sut, simulator.Machine, simulator.Info.Agent);
        Assert.Same(Point(simulator.Machine), simulator.Info.Agent.CurrentlyUsedGameObject);
    }

    [Fact]
    public void FirstReceivedLoad_RefreshesMissionBeforeRetainingFutureAuthority()
    {
        var simulator = Replica("simulator");
        var request = new NetworkMangonelLoad("battle", Guid.NewGuid(), grantId, agentId,
            "loader", 7, 1496, 1487, "simulator", 3, 2, MangonelLoadPhase.Request, "loader");
        AccessTools.Method(typeof(SiegeMachineStateReplicator), "HandleMangonelLoad").Invoke(simulator.Sut,
            new object[] { new MessagePayload<NetworkMangonelLoad>(this, request) });
        GameThread.Run(() => { }, blocking: true);
        Assert.Same(mission.Instance, Field<Mission>(simulator.Sut, "trackedMission"));
        Assert.Single(Field<Dictionary<Guid, NetworkMangonelLoad>>(simulator.Sut, "pendingMangonelLoads"));
        AccessTools.Method(typeof(SiegeMachineStateReplicator), "RefreshMachineCache").Invoke(simulator.Sut, null);
        Assert.Single(Field<Dictionary<Guid, NetworkMangonelLoad>>(simulator.Sut, "pendingMangonelLoads"));
        Field<Dictionary<int, UsableMachine>>(simulator.Sut, "machinesById")[1496] = simulator.Machine;
        Field<Dictionary<int, string>>(simulator.Sut, "claimedMachines")[1496] = "simulator";
        Field<Dictionary<int, int>>(simulator.Sut, "authorityEpochs")[1496] = 3;
        Field<Dictionary<int, int>>(simulator.Sut, "authorityRevisions")[1496] = 2;
        Tick(simulator.Sut);
        Assert.Equal(request.RequestId, Sent(simulator.Network, MangonelLoadPhase.Animate).RequestId);
    }

    [Fact]
    public void RequestBeforeAgentAuthority_WaitsAndOldCleanupDoesNotStopTheNewOwner()
    {
        var simulator = Replica("simulator");
        var request = new NetworkMangonelLoad("battle", Guid.NewGuid(), grantId, agentId,
            "loader", 8, 1496, 1487, "simulator", 3, 2, MangonelLoadPhase.Request, "loader");
        simulator.Sut.ApplyMangonelLoad(request);
        Assert.Null(simulator.Info.Agent.CurrentlyUsedGameObject);
        AccessTools.Property(typeof(CoopAgentInfo), nameof(CoopAgentInfo.AuthorityRevision)).SetValue(simulator.Info, 8L);
        Tick(simulator.Sut);
        Assert.Equal(request.RequestId, Sent(simulator.Network, MangonelLoadPhase.Animate).RequestId);
        BeginAndEndLoad(simulator.Sut, simulator.Machine, simulator.Info.Agent);
        AccessTools.Property(typeof(CoopAgentInfo), nameof(CoopAgentInfo.AuthorityRevision)).SetValue(simulator.Info, 9L);
        Tick(simulator.Sut);
        Assert.Same(Point(simulator.Machine), simulator.Info.Agent.CurrentlyUsedGameObject);
        Assert.Equal(1, simulator.Info.Agent.Equipment[EquipmentIndex.ExtraWeaponSlot].Amount);
    }

    [Fact]
    public void OwnerReadyBeforeAuthority_RetainsItsRequestThroughCatchUp()
    {
        var simulator = Replica("simulator");
        var request = new NetworkMangonelLoad("battle", Guid.NewGuid(), grantId, agentId,
            "loader", 7, 1496, 1487, "simulator", 3, 4, MangonelLoadPhase.Request, "loader");
        simulator.Sut.ApplyMangonelLoad(request);
        simulator.Sut.ApplyMangonelLoad(request.WithPhase(MangonelLoadPhase.Ready, "loader"));
        Field<Dictionary<int, int>>(simulator.Sut, "authorityRevisions")[1496] = 4;
        Tick(simulator.Sut);
        CurrentActions[simulator.Info.Agent] = ActionIndex(12);
        NativeLoadTick(simulator.Sut, simulator.Machine);
        Assert.Same(Point(simulator.Machine), simulator.Info.Agent.CurrentlyUsedGameObject);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public void NativeTick_AttachesOnlyAfterOwnerReadyAndAnUnchangedLoadEnd(bool changedDuringUse, bool staleBegin)
    {
        var simulator = Replica("simulator");
        CurrentActions[simulator.Info.Agent] = ActionIndex(staleBegin ? 11 : 12);
        simulator.Sut.ApplyMangonelLoad(new NetworkMangonelLoad("battle", Guid.NewGuid(), grantId, agentId,
            "loader", 7, 1496, 1487, "simulator", 3, 2, MangonelLoadPhase.Request, "loader"));
        bool oldAuthority = SiegeMissionAuthorityGate.IsLocalAuthority;
        SiegeMissionAuthorityGate.IsLocalAuthority = true;
        Action<MessagePayload<MangonelLoadTick>> receive = payload => NativeLoadTick(simulator.Sut, payload.What.Machine);
        MessageBroker.Instance.Subscribe(receive);
        try
        {
            object[] args = { simulator.Machine, null };
            AccessTools.Method(typeof(MangonelAmmoConsumedPatch), "Prefix").Invoke(null, args);
            Assert.Null(args[1]);
            Assert.Null(Point(simulator.Machine).UserAgent);
            CurrentActions[simulator.Info.Agent] = ActionIndex(12);
            AccessTools.Method(typeof(MangonelAmmoConsumedPatch), "Prefix").Invoke(null, args);
            Assert.Null(args[1]);
            Assert.Null(Point(simulator.Machine).UserAgent);
            CurrentActions[simulator.Info.Agent] = ActionIndex(11);
            AccessTools.Method(typeof(MangonelAmmoConsumedPatch), "Prefix").Invoke(null, args);
            Assert.Null(Point(simulator.Machine).UserAgent);
            CurrentActions[simulator.Info.Agent] = ActionIndex(12);
            AccessTools.Method(typeof(MangonelAmmoConsumedPatch), "Prefix").Invoke(null, args);
            Assert.Null(Point(simulator.Machine).UserAgent);
            var load = Field<Dictionary<Guid, NetworkMangonelLoad>>(simulator.Sut, "mangonelLoads")[agentId];
            simulator.Sut.ApplyMangonelLoad(load.WithPhase(MangonelLoadPhase.Ready, "loader"));
            changeActionOnUse = changedDuringUse;
            AccessTools.Method(typeof(MangonelAmmoConsumedPatch), "Prefix").Invoke(null, args);
            Assert.Equal(!changedDuringUse, Point(simulator.Machine).HasUser);
            if (!changedDuringUse)
            {
                Assert.Same(simulator.Info.Agent, args[1]);
                Assert.Equal(ActionIndex(12), simulator.Info.Agent.GetCurrentAction(1));
            }
            Assert.False(Actions.ContainsKey(simulator.Info.Agent));
            Assert.False(Removals.ContainsKey(simulator.Info.Agent));
        }
        finally
        {
            MessageBroker.Instance.Unsubscribe(receive);
            SiegeMissionAuthorityGate.IsLocalAuthority = oldAuthority;
            changeActionOnUse = false;
        }
    }

    private static void NativeLoadTick(SiegeMachineStateReplicator sut, Mangonel machine) =>
        AccessTools.Method(typeof(SiegeMachineStateReplicator), "HandleMangonelLoadTick").Invoke(sut,
            new object[] { new MessagePayload<MangonelLoadTick>(machine, new MangonelLoadTick(machine)) });

    private static void BeginAndEndLoad(SiegeMachineStateReplicator sut, Mangonel machine, Agent agent)
    {
        CurrentActions[agent] = ActionIndex(11);
        NativeLoadTick(sut, machine);
        Assert.Null(agent.CurrentlyUsedGameObject);
        CurrentActions[agent] = ActionIndex(12);
        NativeLoadTick(sut, machine);
        Assert.Null(agent.CurrentlyUsedGameObject);
        var load = Assert.Single(Field<Dictionary<Guid, NetworkMangonelLoad>>(sut, "mangonelLoads").Values);
        sut.ApplyMangonelLoad(load.WithPhase(MangonelLoadPhase.Ready, "loader"));
        NativeLoadTick(sut, machine);
    }

    private static void Complete(SiegeMachineStateReplicator sut, Mangonel machine, Agent agent) =>
        AccessTools.Method(typeof(SiegeMachineStateReplicator), "HandleMangonelAmmoConsumed").Invoke(sut,
            new object[] { new MessagePayload<MangonelAmmoConsumed>(machine, new MangonelAmmoConsumed(machine, agent)) });
    private static NetworkMangonelLoad Sent(Mock<IBattleNetwork> network, MangonelLoadPhase phase)
    {
        var matches = new List<NetworkMangonelLoad>();
        foreach (var call in network.Invocations)
            foreach (var argument in call.Arguments)
                if (argument is NetworkMangonelLoad load && load.Phase == phase) matches.Add(load);
        return Assert.Single(matches);
    }
    private static void Tick(SiegeMachineStateReplicator sut)
    {
        AccessTools.Method(typeof(SiegeMachineStateReplicator), "ObserveMangonelLoadActions").Invoke(sut, null);
        AccessTools.Method(typeof(SiegeMachineStateReplicator), "TickMangonelLoads").Invoke(sut, null);
    }
    private static T Field<T>(object target, string name) => (T)AccessTools.Field(target.GetType(), name).GetValue(target);
    private static StandingPoint Point(Mangonel machine) => Field<StandingPoint>(machine, "LoadAmmoStandingPoint");
    private static void SetState(Mangonel machine, RangedSiegeWeapon.WeaponState state) =>
        AccessTools.Field(typeof(RangedSiegeWeapon), "_state").SetValue(machine, state);
    private void Stub(Type type, string name, string prefix) => harmony.Patch(AccessTools.Method(type, name),
        prefix: new HarmonyMethod(typeof(MangonelLoadReplicationTests), prefix));
    private static bool Skip() => false;
    private static bool Active(ref bool __result) { __result = true; return false; }
    private static bool MainHand(ref EquipmentIndex __result) { __result = EquipmentIndex.ExtraWeaponSlot; return false; }
    private static bool OffHand(ref EquipmentIndex __result) { __result = EquipmentIndex.None; return false; }
    private static bool ActionCode(ref int __result) { __result = 0; return false; }
    private static ActionIndexCache ActionIndex(int index)
    {
        object action = default(ActionIndexCache);
        AccessTools.Field(typeof(ActionIndexCache), "<Index>k__BackingField").SetValue(action, index);
        return (ActionIndexCache)action;
    }
    private static bool CurrentAction(Agent __instance, ref ActionIndexCache __result)
    {
        CurrentActions.TryGetValue(__instance, out __result);
        return false;
    }
    private static bool Use(Agent __instance, UsableMissionObject __0)
    {
        AccessTools.Property(typeof(Agent), "CurrentlyUsedGameObject").SetValue(__instance, __0);
        Users[(StandingPoint)__0] = __instance;
        if (changeActionOnUse) CurrentActions[__instance] = default;
        return false;
    }
    private static bool Stop(Agent __instance)
    {
        if (__instance.CurrentlyUsedGameObject is StandingPoint point)
            Users.Remove(point);
        AccessTools.Property(typeof(Agent), "CurrentlyUsedGameObject").SetValue(__instance, null);
        return false;
    }
    private static bool Action(Agent __instance, ActionIndexCache __1, ref bool __result)
    {
        Actions.TryGetValue(__instance, out int count);
        Actions[__instance] = count + 1;
        CurrentActions[__instance] = __1;
        __result = true;
        return false;
    }
    private static bool User(UsableMissionObject __instance, ref Agent __result)
    {
        Users.TryGetValue(__instance, out __result);
        return false;
    }
    private static bool HasUser(UsableMissionObject __instance, ref bool __result)
    {
        __result = Users.ContainsKey(__instance);
        return false;
    }
    private static bool Remove(Agent __instance, EquipmentIndex __0)
    {
        Removals.TryGetValue(__instance, out int count);
        Removals[__instance] = count + 1;
        __instance.Equipment[__0] = default;
        return false;
    }
#pragma warning disable SYSLIB0050
    private static T New<T>() => (T)FormatterServices.GetUninitializedObject(typeof(T));
#pragma warning restore SYSLIB0050
    public void Dispose()
    {
        foreach (var sut in replicas) sut.Dispose();
        harmony.UnpatchAll(harmony.Id);
        mission.Dispose();
        Actions.Clear();
        Removals.Clear();
        Users.Clear();
        CurrentActions.Clear();
    }
}
