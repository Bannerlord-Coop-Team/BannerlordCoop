using Common.Serialization;
using Common.PacketHandlers;
using Common.Messaging;
using HarmonyLib;
using GameInterface;
using GameInterface.Services.MapEvents;
using Missions;
using Missions.Agents.Packets;
using Missions.Agents.Patches;
using Missions.Battles;
using Missions.Messages;
using Missions.Services.Network;
using E2E.Tests.Environment.MockEngine;
using System;
using System.Linq;
using System.Runtime.Serialization;
using TaleWorlds.Core;
using TaleWorlds.Engine;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
using Xunit;

namespace E2E.Tests.Services.Missions;

public partial class ActionEquipmentSyncTests
{
    [Fact]
    public void PilotSeat_UseAndStopPublishWithoutAnimationChangeAndCatchUpIncludesSeat()
    {
        RunScenario(context =>
        {
            using var seats = new PilotSeatFixture(context, "peer");
            var agent = seats.Spawn("peer", out var id);
            context.Component.AgentActionHandler.PollActionsAfterNativeTick();
            Assert.Empty(context.Network.NetworkSentPackets.GetPackets<AgentActionPacket>());
            agent.UseGameObject(seats.Point);
            context.Component.AgentActionHandler.PollActionsAfterNativeTick();
            var first = Assert.Single(context.Network.NetworkSentPackets.GetPackets<AgentActionPacket>());
            Assert.True(Assert.Single(first.Actions).PilotSeat.Value.Using);
            context.Component.AgentActionHandler.PollActionsAfterNativeTick();
            Assert.Single(context.Network.NetworkSentPackets.GetPackets<AgentActionPacket>());
            context.Component.AgentActionHandler.CatchUpJoiner("joiner");
            Drain();
            var catchUp = Assert.IsType<AgentActionPacket>(Assert.Single(context.Network.DirectPacketSends).Packet);
            Assert.Equal(first.Actions[0].PilotSeat, catchUp.Actions[0].PilotSeat);
            context.Network.NetworkSentPackets.Packets.Clear();
            agent.StopUsingGameObject();
            Assert.Null(agent.CurrentlyUsedGameObject);
            Assert.Null(seats.Point.UserAgent);
            context.Component.AgentActionHandler.PollActionsAfterNativeTick();
            var stopped = Assert.Single(context.Network.NetworkSentPackets.GetPackets<AgentActionPacket>());
            Assert.False(stopped.Actions[0].PilotSeat.Value.Using);
        });
    }

    [Fact]
    public void PilotSeat_WireBeginAndReleaseMaintainExactOccupancyAndIgnoreOlderBegin()
    {
        RunScenario(context =>
        {
            using var seats = new PilotSeatFixture(context);
            var agent = seats.Spawn("owner", out var id);
            var begin = seats.Packet(agent, id, 1);
            var serializer = new ProtoBufSerializer(new SerializableTypeMapper());
            context.Receive(Assert.IsType<AgentActionPacket>(serializer.Deserialize<IPacket>(serializer.Serialize(begin))));
            Assert.Same(seats.Point, agent.CurrentlyUsedGameObject);
            Assert.Same(agent, seats.Point.UserAgent);
            context.Receive(seats.Packet(agent, id, 2, usingSeat: false));
            context.Receive(begin);
            Assert.Null(agent.CurrentlyUsedGameObject);
            Assert.Null(seats.Point.UserAgent);
        });
    }

    [Theory]
    [InlineData("machine", false)]
    [InlineData("agent", false)]
    [InlineData("point", false)]
    [InlineData("authority", false)]
    [InlineData("machine", true)]
    [InlineData("agent", true)]
    [InlineData("point", true)]
    [InlineData("authority", true)]
    public void PilotSeat_BeginWaitsForDependencyAndReleaseSupersedesPendingBegin(string missing, bool released)
    {
        RunScenario(context =>
        {
            using var seats = new PilotSeatFixture(context);
            var agent = seats.Spawn("owner", out var id);
            if (missing == "machine") seats.Objects.Remove(seats.Machine);
            if (missing == "agent") context.Registry.RemoveAgent(id);
            if (missing == "point") seats.Machine.StandingPoints.Clear();
            if (missing == "authority") seats.Authority.Revision = 0;
            context.Receive(seats.Packet(agent, id, 1));
            Assert.Null(agent.CurrentlyUsedGameObject);
            if (released) context.Receive(seats.Packet(agent, id, 2, usingSeat: false));
            if (missing == "machine") seats.Objects.Add(seats.Machine);
            if (missing == "agent") seats.Register(agent, id, "owner");
            if (missing == "point") seats.Machine.StandingPoints.Add(seats.Point);
            seats.Authority.Revision = 1;
            context.Component.AgentActionHandler.ApplyRemoteGuardStates();
            Assert.Equal(released ? null : seats.Point, agent.CurrentlyUsedGameObject);
            context.Receive(seats.Packet(agent, id, 3));
            Assert.Same(seats.Point, agent.CurrentlyUsedGameObject);
        });
    }

    [Fact]
    public void PilotSeat_BeginBeforeBindingRetainsOrderedSnapshot()
    {
        RunScenario(context =>
        {
            using var seats = new PilotSeatFixture(context);
            var agent = seats.Spawn("owner", out var id);
            Assert.True(AgentMirror.TryGet(agent, out var puppet));
            var owner = context.Spawn("owner", out var ownerMirror, out _);
            ownerMirror.Action0Index = 1001;
            ownerMirror.Action0CodeType = Agent.ActionCodeType.ReleaseMelee;
            context.Component.AgentActionHandler.BindPilotSeats(null, null);
            context.Receive(seats.Packet(owner, id, 1));
            Assert.Null(agent.CurrentlyUsedGameObject);
            Assert.Equal(0, puppet.SetActionChannelCalls);
            context.Component.AgentActionHandler.BindPilotSeats("pilot-battle", seats.Authority);
            context.Component.AgentActionHandler.ApplyRemoteGuardStates();
            Assert.Same(seats.Point, agent.CurrentlyUsedGameObject);
            Assert.Equal(1001, puppet.Action0Index);
        });
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void PilotSeat_ConflictingOccupantIsNeverEvicted(bool localOccupant)
    {
        RunScenario(context =>
        {
            using var seats = new PilotSeatFixture(context);
            var agent = seats.Spawn("owner", out var id);
            var contender = seats.Spawn(localOccupant ? "peer" : "other", out _);
            contender.UseGameObject(seats.Point);
            context.Receive(seats.Packet(agent, id, 1));
            Assert.Null(agent.CurrentlyUsedGameObject);
            Assert.Same(contender, seats.Point.UserAgent);
            contender.StopUsingGameObject();
            context.Component.AgentActionHandler.ApplyRemoteGuardStates();
            Assert.Same(agent, seats.Point.UserAgent);
        });
    }

    [Theory]
    [InlineData("occupied", false)]
    [InlineData("moving", false)]
    [InlineData("loading", false)]
    [InlineData("occupied", true)]
    [InlineData("moving", true)]
    [InlineData("loading", true)]
    public void PilotSeat_ConflictDoesNotBlockEquipmentOrActionsAndRetainsOnlyLatestSeat(string conflict, bool stopped)
    {
        RunScenario(context =>
        {
            using var seats = new PilotSeatFixture(context);
            var agent = seats.Spawn("owner", out var id);
            Assert.True(AgentMirror.TryGet(agent, out var puppet));
            var owner = context.Spawn("owner", out var ownerMirror, out _);
            var contender = seats.Spawn("peer", out _);
            var loadingPoint = NewPilotObject<StandingPointWithWeaponRequirement>();
            loadingPoint.Id = new MissionObjectId(883, false);
            seats.Machine.LoadAmmoStandingPoint = loadingPoint;
            seats.Machine.StandingPoints.Add(loadingPoint);
            if (conflict == "occupied") contender.UseGameObject(seats.Point);
            if (conflict == "moving") seats.Point.MovingAgent = contender;
            if (conflict == "loading") agent.UseGameObject(loadingPoint);
            ownerMirror.Equipment[EquipmentIndex.Weapon1] = Weapon("sword");
            puppet.Equipment[EquipmentIndex.Weapon1] = ownerMirror.Equipment[EquipmentIndex.Weapon1];
            ownerMirror.PrimaryWieldedItemIndex = EquipmentIndex.Weapon1;
            ownerMirror.Action0Index = 1001;
            ownerMirror.Action0CodeType = Agent.ActionCodeType.ReleaseMelee;
            ownerMirror.EventControlFlags = (Agent.EventControlFlag)1;
            var begin = seats.Packet(owner, id, 1, includeEquipment: true);
            context.Receive(begin);
            Assert.Equal(EquipmentIndex.Weapon1, agent.GetPrimaryWieldedItemIndex());
            Assert.Equal(1001, puppet.Action0Index);
            Assert.Equal(conflict == "loading" ? loadingPoint : null, agent.CurrentlyUsedGameObject);
            Assert.Equal(conflict == "occupied" ? contender : null, seats.Point.UserAgent);
            Assert.Equal(conflict == "moving" ? contender : null, seats.Point.MovingAgent);
            ownerMirror.Action0Index = 1002;
            context.Receive(seats.Packet(owner, id, 2, usingSeat: !stopped, includeEquipment: true));
            Assert.Equal(1002, puppet.Action0Index);
            int calls = puppet.SetActionChannelCalls;
            context.Component.AgentActionHandler.ApplyRemoteGuardStates();
            Assert.Equal(calls, puppet.SetActionChannelCalls);
            if (conflict == "occupied") contender.StopUsingGameObject();
            if (conflict == "moving") seats.Point.MovingAgent = null;
            if (conflict == "loading") agent.StopUsingGameObject();
            if (!stopped) puppet.Action0Index = 1003;
            puppet.EventControlFlags = Agent.EventControlFlag.None;
            context.Component.AgentActionHandler.ApplyRemoteGuardStates();
            Assert.Equal(stopped ? null : seats.Point, agent.CurrentlyUsedGameObject);
            Assert.Equal(1002, puppet.Action0Index);
            Assert.Equal(Agent.EventControlFlag.None, puppet.EventControlFlags);
            context.Receive(begin);
            Assert.Equal(stopped ? null : seats.Point, agent.CurrentlyUsedGameObject);
            Assert.Equal(1002, puppet.Action0Index);
        });
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void PilotSeat_DeferredAttachmentRestoresRetainedEquipmentAfterNativeUseEnds(bool missingWeapon)
    {
        RunScenario(context =>
        {
            using var seats = new PilotSeatFixture(context);
            var agent = seats.Spawn("owner", out var id);
            Assert.True(AgentMirror.TryGet(agent, out var puppet));
            var owner = context.Spawn("owner", out var ownerMirror, out _);
            var loadingPoint = NewPilotObject<StandingPointWithWeaponRequirement>();
            agent.UseGameObject(loadingPoint);
            ownerMirror.Equipment[EquipmentIndex.Weapon1] = Weapon("sword");
            puppet.Equipment[EquipmentIndex.Weapon1] = ownerMirror.Equipment[EquipmentIndex.Weapon1];
            ownerMirror.PrimaryWieldedItemIndex = EquipmentIndex.Weapon1;
            ownerMirror.Action0Index = 1001;
            ownerMirror.Action0CodeType = Agent.ActionCodeType.ReleaseMelee;
            ownerMirror.EventControlFlags = (Agent.EventControlFlag)1;
            var begin = seats.Packet(owner, id, 1, includeEquipment: true);
            context.Receive(begin);
            context.Receive(new AgentActionPacket("owner", new[] { id },
                new[] { begin.Actions[0].WithEquipment(1, null) }, new[] { 2L }));
            Assert.Equal(EquipmentIndex.Weapon1, agent.GetPrimaryWieldedItemIndex());
            agent.StopUsingGameObject();
            // Native object release can restore the hands saved before that use.
            puppet.PrimaryWieldedItemIndex = EquipmentIndex.None;
            puppet.Action0Index = 1002;
            puppet.EventControlFlags = Agent.EventControlFlag.None;
            int calls = puppet.SetActionChannelCalls;
            if (missingWeapon) puppet.Equipment[EquipmentIndex.Weapon1] = default;
            context.Component.AgentActionHandler.ApplyRemoteGuardStates();
            if (missingWeapon)
            {
                Assert.Equal(calls, puppet.SetActionChannelCalls);
                Assert.Equal(EquipmentIndex.None, agent.GetPrimaryWieldedItemIndex());
                puppet.Equipment[EquipmentIndex.Weapon1] = ownerMirror.Equipment[EquipmentIndex.Weapon1];
                context.Component.AgentActionHandler.ApplyRemoteGuardStates();
            }
            Assert.Same(seats.Point, agent.CurrentlyUsedGameObject);
            Assert.Equal(EquipmentIndex.Weapon1, agent.GetPrimaryWieldedItemIndex());
            Assert.Equal(1001, puppet.Action0Index);
            Assert.Equal(Agent.EventControlFlag.None, puppet.EventControlFlags);
            calls = puppet.SetActionChannelCalls;
            context.Component.AgentActionHandler.ApplyRemoteGuardStates();
            Assert.Equal(calls, puppet.SetActionChannelCalls);
        });
    }

    [Theory]
    [InlineData("machine")]
    [InlineData("agent")]
    [InlineData("mission")]
    public void PilotSeat_DeferredConflictCannotAttachAfterAuthorityOrMissionChange(string change)
    {
        RunScenario(context =>
        {
            using var seats = new PilotSeatFixture(context);
            var agent = seats.Spawn("owner", out var id);
            var contender = seats.Spawn("peer", out _);
            contender.UseGameObject(seats.Point);
            context.Receive(seats.Packet(agent, id, 1));
            if (change == "machine") seats.Authority.Revision++;
            if (change == "agent") Assert.True(context.Registry.TryTransferAuthority("owner", id, 1));
            if (change == "mission") context.Component.AgentActionHandler.BindPilotSeats(null, null);
            contender.StopUsingGameObject();
            context.Component.AgentActionHandler.ApplyRemoteGuardStates();
            Assert.Null(agent.CurrentlyUsedGameObject);
            Assert.Null(seats.Point.UserAgent);
        });
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void PilotSeat_NewerStopCancelsDeferredSeatWhileEquipmentIsPending(bool missingBaseline)
    {
        RunScenario(context =>
        {
            using var seats = new PilotSeatFixture(context);
            var agent = seats.Spawn("owner", out var id);
            var owner = context.Spawn("owner", out var ownerMirror, out _);
            var contender = seats.Spawn("peer", out _);
            contender.UseGameObject(seats.Point);
            context.Receive(seats.Packet(owner, id, 1));
            ownerMirror.Equipment[EquipmentIndex.Weapon1] = Weapon("sword");
            ownerMirror.PrimaryWieldedItemIndex = EquipmentIndex.Weapon1;
            var stop = seats.Packet(owner, id, 2, usingSeat: false, includeEquipment: true);
            if (missingBaseline)
                stop = new AgentActionPacket("owner", new[] { id },
                    new[] { stop.Actions[0].WithEquipment(2, null) }, new[] { 2L });
            context.Component.AgentActionHandler.HandlePacket(null, stop);
            Drain();
            contender.StopUsingGameObject();
            seats.Authority.Changed();
            Assert.Null(agent.CurrentlyUsedGameObject);
            Assert.Null(seats.Point.UserAgent);
        });
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void PilotSeat_NewerAuthoritySnapshotCancelsDeferredEntryBeforeItCanApply(bool stopped)
    {
        RunScenario(context =>
        {
            using var seats = new PilotSeatFixture(context);
            var agent = seats.Spawn("owner", out var id);
            var contender = seats.Spawn("peer", out _);
            contender.UseGameObject(seats.Point);
            context.Receive(seats.Packet(agent, id, 1));
            context.Receive(seats.Packet(agent, id, 2, usingSeat: !stopped, agentRevision: 1));
            contender.StopUsingGameObject();
            context.Component.AgentActionHandler.ApplyRemoteGuardStates();
            Assert.Null(agent.CurrentlyUsedGameObject);
            Assert.Null(seats.Point.UserAgent);
            Assert.True(context.Registry.TryTransferAuthority("owner", id, 1));
            context.Component.AgentActionHandler.ApplyRemoteGuardStates();
            Assert.Equal(stopped ? null : seats.Point, agent.CurrentlyUsedGameObject);
        });
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void PilotSeat_NewerHostSnapshotCancelsDeferredEntryBeforeAssignment(bool stopped)
    {
        RunScenario(context =>
        {
            using var seats = new PilotSeatFixture(context);
            var agent = seats.Spawn("owner", out var id);
            var contender = seats.Spawn("peer", out _);
            BattleSpawnGate.BeginBattle("pilot-battle");
            try
            {
                var hosts = context.Instance.Resolve<IBattleHostRegistry>();
                hosts.Set("pilot-battle", new BattleHostAssignment("owner", Array.Empty<string>(), 1));
                contender.UseGameObject(seats.Point);
                context.Receive(seats.Packet(agent, id, 1));
                context.Receive(seats.Packet(agent, id, 2, usingSeat: !stopped, hostEpoch: 2));
                contender.StopUsingGameObject();
                context.Component.AgentActionHandler.ApplyRemoteGuardStates();
                Assert.Null(agent.CurrentlyUsedGameObject);
                Assert.Null(seats.Point.UserAgent);
                hosts.Set("pilot-battle", new BattleHostAssignment("owner", Array.Empty<string>(), 2));
                context.Component.AgentActionHandler.ApplyRemoteGuardStates();
                Assert.Equal(stopped ? null : seats.Point, agent.CurrentlyUsedGameObject);
            }
            finally { BattleSpawnGate.EndBattle(); }
        });
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void PilotSeat_StaleOrOtherControllerSnapshotCannotCancelDeferredEntry(bool otherController)
    {
        RunScenario(context =>
        {
            using var seats = new PilotSeatFixture(context);
            var agent = seats.Spawn("owner", out var id);
            var contender = seats.Spawn("peer", out _);
            contender.UseGameObject(seats.Point);
            context.Receive(seats.Packet(agent, id, 1));
            var stop = seats.Packet(agent, id, 1, usingSeat: false);
            if (otherController)
            {
                stop = seats.Packet(agent, id, 2, usingSeat: false, agentRevision: 1);
                stop = new AgentActionPacket("other", stop.AgentIds, stop.Actions, stop.Sequences);
            }
            context.Receive(stop);
            contender.StopUsingGameObject();
            context.Component.AgentActionHandler.ApplyRemoteGuardStates();
            Assert.Same(seats.Point, agent.CurrentlyUsedGameObject);
            Assert.Same(agent, seats.Point.UserAgent);
        });
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void PilotSeat_HostScopedFutureAgentRevisionRetainsSeatUntilAuthorityArrives(bool stopped)
    {
        RunScenario(context =>
        {
            using var seats = new PilotSeatFixture(context);
            var agent = seats.Spawn("owner", out var id);
            BattleSpawnGate.BeginBattle("pilot-battle");
            try
            {
                context.Instance.Resolve<IBattleHostRegistry>().Set("pilot-battle",
                    new BattleHostAssignment("owner", Array.Empty<string>(), 1));
                context.Receive(seats.Packet(agent, id, 1, agentRevision: 1, hostEpoch: 1));
                Assert.Null(agent.CurrentlyUsedGameObject);
                if (stopped) context.Receive(seats.Packet(agent, id, 2, usingSeat: false, agentRevision: 1, hostEpoch: 1));
                Assert.True(context.Registry.TryTransferAuthority("owner", id, 1));
                context.Component.AgentActionHandler.ApplyRemoteGuardStates();
                Assert.Equal(stopped ? null : seats.Point, agent.CurrentlyUsedGameObject);
            }
            finally { BattleSpawnGate.EndBattle(); }
        });
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void PilotSeat_MigratedHostWaitsForRegistryOwnerBeforeApplyingSeat(bool stopped)
    {
        RunScenario(context =>
        {
            using var seats = new PilotSeatFixture(context);
            var agent = seats.Spawn("old-owner", out var id);
            BattleSpawnGate.BeginBattle("pilot-battle");
            try
            {
                context.Instance.Resolve<IBattleHostRegistry>().Set("pilot-battle",
                    new BattleHostAssignment("owner", Array.Empty<string>(), 1));
                context.Instance.Resolve<IMessageBroker>().Publish(this,
                    new NetworkBattleHostAssigned("pilot-battle", "owner", Array.Empty<string>(), 1));
                Drain();
                context.Receive(seats.Packet(agent, id, 1, agentRevision: 1, hostEpoch: 1));
                Assert.Null(agent.CurrentlyUsedGameObject);
                if (stopped) context.Receive(seats.Packet(agent, id, 2, usingSeat: false, agentRevision: 1, hostEpoch: 1));
                Assert.True(context.Registry.TryTransferAuthority("owner", id, 1));
                context.Component.AgentActionHandler.ApplyRemoteGuardStates();
                Assert.Equal(stopped ? null : seats.Point, agent.CurrentlyUsedGameObject);
            }
            finally { BattleSpawnGate.EndBattle(); }
        });
    }

    [Theory]
    [InlineData("epoch")]
    [InlineData("machine-revision")]
    [InlineData("agent-revision")]
    [InlineData("agent-owner")]
    [InlineData("removal")]
    public void PilotSeat_AuthorityOrAgentLossClearsOnlyAcceptedSeat(string change)
    {
        RunScenario(context =>
        {
            using var seats = new PilotSeatFixture(context);
            var agent = seats.Spawn("owner", out var id);
            context.Receive(seats.Packet(agent, id, 1));
            Assert.Same(agent, seats.Point.UserAgent);
            if (change == "epoch") seats.Authority.Epoch++;
            if (change == "machine-revision") seats.Authority.Revision++;
            if (change == "agent-revision") context.Registry.TryTransferAuthority("owner", id, 1);
            if (change == "agent-owner") context.Registry.TryTransferAuthority("new-owner", id, 1);
            if (change == "removal") context.Registry.RemoveAgent(id);
            seats.Authority.Changed();
            context.Component.AgentActionHandler.ApplyRemoteGuardStates();
            Assert.Null(agent.CurrentlyUsedGameObject);
            Assert.Null(seats.Point.UserAgent);
            context.Receive(seats.Packet(agent, id, 2));
            Assert.Null(agent.CurrentlyUsedGameObject);
        });
    }

    [Fact]
    public void PilotSeat_MangonelLoadPointAndUntrackedUseRemainUntouched()
    {
        RunScenario(context =>
        {
            using var seats = new PilotSeatFixture(context);
            var agent = seats.Spawn("owner", out var id);
            var loaderPoint = NewPilotObject<StandingPointWithWeaponRequirement>();
            loaderPoint.Id = new MissionObjectId(883, false);
            seats.Machine.LoadAmmoStandingPoint = loaderPoint;
            seats.Machine.StandingPoints.Add(loaderPoint);
            agent.UseGameObject(loaderPoint);
            context.Receive(seats.Packet(agent, id, 1, pointId: 883));
            context.Receive(seats.Packet(agent, id, 2, usingSeat: false));
            Assert.Same(loaderPoint, agent.CurrentlyUsedGameObject);
            Assert.Same(agent, loaderPoint.UserAgent);
            context.Component.AgentActionHandler.BindPilotSeats(null, null);
            Assert.Same(loaderPoint, agent.CurrentlyUsedGameObject);
        });
    }

    [Fact]
    public void PilotSeat_OldReleaseCannotClearReattachedNewAuthorityOrDifferentCurrentObject()
    {
        RunScenario(context =>
        {
            using var seats = new PilotSeatFixture(context);
            var agent = seats.Spawn("owner", out var id);
            context.Receive(seats.Packet(agent, id, 1));
            seats.Authority.Revision = 2;
            seats.Authority.Changed();
            context.Receive(seats.Packet(agent, id, 2, machineRevision: 2));
            context.Receive(seats.Packet(agent, id, 3, usingSeat: false));
            Assert.Same(seats.Point, agent.CurrentlyUsedGameObject);
            agent.StopUsingGameObject();
            var other = NewPilotObject<StandingPoint>();
            agent.UseGameObject(other);
            context.Component.AgentActionHandler.BindPilotSeats(null, null);
            Assert.Same(other, agent.CurrentlyUsedGameObject);
        });
    }

    [Fact]
    public void PilotSeat_FutureAgentRevisionWaitsAndOldOwnerCannotReattach()
    {
        RunScenario(context =>
        {
            using var seats = new PilotSeatFixture(context);
            var agent = seats.Spawn("owner", out var id);
            context.Receive(seats.Packet(agent, id, 1, agentRevision: 1));
            Assert.Null(agent.CurrentlyUsedGameObject);
            Assert.True(context.Registry.TryTransferAuthority("owner", id, 1));
            context.Component.AgentActionHandler.ApplyRemoteGuardStates();
            Assert.Same(seats.Point, agent.CurrentlyUsedGameObject);
            Assert.True(context.Registry.TryTransferAuthority("other", id, 2));
            context.Component.AgentActionHandler.ApplyRemoteGuardStates();
            context.Receive(seats.Packet(agent, id, 10, agentRevision: 1));
            Assert.Null(agent.CurrentlyUsedGameObject);
        });
    }

    [Theory]
    [InlineData("battle")]
    [InlineData("machine-owner")]
    [InlineData("non-pilot")]
    [InlineData("duplicate-machine")]
    public void PilotSeat_RejectsWrongIdentity(string change)
    {
        RunScenario(context =>
        {
            using var seats = new PilotSeatFixture(context);
            var agent = seats.Spawn("owner", out var id);
            if (change == "battle") context.Component.AgentActionHandler.BindPilotSeats("other-battle", seats.Authority);
            if (change == "machine-owner") seats.Authority.Owner = "other";
            if (change == "non-pilot") seats.Machine.PilotStandingPoint = null;
            if (change == "duplicate-machine")
            {
                var duplicate = NewPilotObject<Ballista>();
                duplicate.Id = seats.Machine.Id;
                seats.Objects.Add(duplicate);
            }
            context.Receive(seats.Packet(agent, id, 1));
            Assert.Null(agent.CurrentlyUsedGameObject);
            Assert.Null(seats.Point.UserAgent);
        });
    }

    private static T NewPilotObject<T>() => (T)FormatterServices.GetUninitializedObject(typeof(T));

    [Fact]
    public void MissionModule_InstallsReplicatedPilotPointGuard()
    {
        HarmonyPatchCategoryRegistration registration = Assert.Single(
            MissionModule.CreatePatchCategoryRegistrations(),
            candidate => candidate.Category == MissionModule.PilotSeatPatchCategory);
        Assert.Equal(typeof(ReplicatedPilotPointPatch).Assembly, registration.Assembly);
        var harmony = new Harmony($"{nameof(MissionModule_InstallsReplicatedPilotPointGuard)}.{Guid.NewGuid()}");
        var target = AccessTools.Method(typeof(StandingPoint), "TickAux");
        try
        {
            registration.Apply(harmony);
            Assert.Contains(Harmony.GetPatchInfo(target).Prefixes, patch =>
                patch.owner == harmony.Id && patch.PatchMethod.DeclaringType == typeof(ReplicatedPilotPointPatch));
        }
        finally { harmony.UnpatchAll(harmony.Id); }
    }

    [Fact]
    public void PilotSeat_NativePointTickOnlyStandsDownForAcceptedPuppetAttachment()
    {
        RunScenario(context =>
        {
            using var seats = new PilotSeatFixture(context);
            var agent = seats.Spawn("owner", out var id);
            var patchType = typeof(CoopAgentInfo).Assembly
                .GetType("Missions.Agents.Patches.ReplicatedPilotPointPatch");
            var prefix = AccessTools.Method(patchType, "Prefix");
            bool enabled = BattleSpawnConfig.Enabled;
            BattleSpawnConfig.Enabled = true;
            BattleSpawnGate.BeginBattle("pilot-battle");
            try
            {
                agent.UseGameObject(seats.Point);
                Assert.True((bool)prefix.Invoke(null, new object[] { seats.Point }));
                agent.StopUsingGameObject();
                context.Receive(seats.Packet(agent, id, 1));
                Assert.False((bool)prefix.Invoke(null, new object[] { seats.Point }));
                seats.Authority.Revision++;
                seats.Authority.Changed();
                Assert.True((bool)prefix.Invoke(null, new object[] { seats.Point }));
            }
            finally
            {
                BattleSpawnGate.EndBattle();
                BattleSpawnConfig.Enabled = enabled;
            }
        });
    }

    private sealed class PilotAuthority : ISiegeMachineStateReplicator
    {
        public string Owner = "owner";
        public int Epoch = 1;
        public int Revision = 1;
        public event Action<int> AuthorityChanged;
        public void Changed() => AuthorityChanged?.Invoke(884);
        public bool TryGetMachineAuthority(int id, out string owner, out int epoch, out int revision)
        { owner = Owner; epoch = Epoch; revision = Revision; return true; }
        public void Tick(float dt) { }
        public void CatchUpJoiner(string id) { }
        public void Dispose() { }
#if DEBUG
        public object ObserveMachineAuthority(UsableMachine machine) => null;
#endif
    }

    private sealed class PilotSeatFixture : IDisposable
    {
        private readonly Context context;
        private readonly Harmony harmony = new("pilot-seat-tests." + Guid.NewGuid());
        public readonly PilotAuthority Authority = new();
        public readonly Ballista Machine;
        public readonly StandingPoint Point;
        public MBList<MissionObject> Objects => (MBList<MissionObject>)AccessTools.Field(typeof(Mission), "_missionObjects").GetValue(Mission.Current);

        public PilotSeatFixture(Context context, string owner = "owner")
        {
            this.context = context;
            harmony.Patch(AccessTools.Method(typeof(ScriptComponentBehavior), "CacheEditableFieldsForAllScriptComponents"),
                prefix: new HarmonyMethod(typeof(PilotSeatFixture), nameof(SkipNativeScriptCache)));
            Machine = NewPilotObject<Ballista>();
            Point = NewPilotObject<StandingPoint>();
            AccessTools.Field(typeof(Mission), "_missionObjects").SetValue(Mission.Current, new MBList<MissionObject>());
            Authority.Owner = owner;
            Point.Id = new MissionObjectId(882, false);
            Machine.Id = new MissionObjectId(884, false);
            Machine.PilotStandingPoint = Point;
            Machine.StandingPoints = new MBList<StandingPoint> { Point };
            Objects.Add(Machine);
            harmony.Patch(AccessTools.Method(typeof(Agent), nameof(Agent.UseGameObject)),
                postfix: new HarmonyMethod(typeof(PilotSeatFixture), nameof(Used)));
            harmony.Patch(AccessTools.Method(typeof(Agent), nameof(Agent.StopUsingGameObject)),
                prefix: new HarmonyMethod(typeof(PilotSeatFixture), nameof(Stopping)) { priority = Priority.First });
            context.Component.AgentActionHandler.BindPilotSeats("pilot-battle", Authority);
        }

        public Agent Spawn(string owner, out Guid id)
        {
            var agent = context.Spawn(owner, out var mirror, out id);
            context.Registry.RemoveAgent(id);
            Register(agent, id, owner);
            if (owner == "peer") mirror.Controller = AgentControllerType.Player;
            return agent;
        }

        public void Register(Agent agent, Guid id, string owner) =>
            Assert.True(context.Registry.TryRegisterAgent(owner, owner, "pilot-battle", id, 0, agent));

        public AgentActionPacket Packet(Agent agent, Guid id, long sequence, bool usingSeat = true,
            int pointId = 882, int machineRevision = 1, long agentRevision = 0,
            int hostEpoch = 0, bool includeEquipment = false)
        {
            var data = new AgentActionData(agent).WithEquipment(includeEquipment ? sequence : 0,
                    includeEquipment ? new AgentEquipmentData(agent) : (AgentEquipmentData?)null,
                    hostEpoch > 0 ? 0 : agentRevision)
                .WithPilotSeat(new AgentPilotSeatData("pilot-battle", 884, pointId, 1, machineRevision, agentRevision, usingSeat));
            return new AgentActionPacket("owner", new[] { id }, new[] { data }, new[] { sequence }, hostEpoch);
        }

        private static void Used(Agent __instance, UsableMissionObject usedObject) =>
            AccessTools.Field(typeof(UsableMissionObject), "_userAgent").SetValue(usedObject, __instance);

        private static bool SkipNativeScriptCache() => false;

        private static void Stopping(Agent __instance)
        {
            var point = __instance.CurrentlyUsedGameObject;
            if (point != null && point.UserAgent == __instance)
                AccessTools.Field(typeof(UsableMissionObject), "_userAgent").SetValue(point, null);
        }

        public void Dispose()
        {
            context.Component.AgentActionHandler.BindPilotSeats(null, null);
            harmony.UnpatchAll(harmony.Id);
        }
    }
}
