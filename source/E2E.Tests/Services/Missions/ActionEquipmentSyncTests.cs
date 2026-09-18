using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Common;
using HarmonyLib;
using Common.Messaging;
using Common.Util;
using Common.PacketHandlers;
using Common.Serialization;
using E2E.Tests.Environment.Instance;
using E2E.Tests.Environment.Mock;
using E2E.Tests.Environment.MockEngine;
using GameInterface.Services.MapEvents;
using Missions;
using Missions.Messages;
using Missions.Agents.Handlers;
using Missions.Agents.Packets;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;
using Xunit;
using Xunit.Abstractions;

namespace E2E.Tests.Services.Missions;

public class ActionEquipmentSyncTests : MissionTestEnvironment
{
    public ActionEquipmentSyncTests(ITestOutputHelper output) : base(output, 3) { }

    [Fact]
    public void PreviouslyPatchedEquipmentApply_UsesCurrentMissionWieldBoundary()
    {
        var warmup = new Harmony("action-equipment-warmup");
        var method = AccessTools.Method(typeof(AgentEquipmentData), nameof(AgentEquipmentData.Apply));
        try
        {
            warmup.Patch(method, postfix: new HarmonyMethod(
                AccessTools.Method(typeof(ActionEquipmentSyncTests), nameof(EquipmentWarmupPostfix))));
            default(AgentEquipmentData).Apply(null);
        }
        finally
        {
            warmup.UnpatchAll(warmup.Id);
        }
        RunScenario(context =>
        {
            var agent = context.Spawn("peer", out var mirror, out _);
            mirror.Equipment[EquipmentIndex.Weapon0] = Weapon("sword");
            mirror.PrimaryWieldedItemIndex = EquipmentIndex.None;
            var equipment = new AgentEquipmentData(EquipmentIndex.Weapon0, EquipmentIndex.None, 0);
            Assert.True(equipment.TryApplyForAction(agent));
            Assert.Equal(EquipmentIndex.Weapon0, mirror.PrimaryWieldedItemIndex);
            Assert.Contains("wield", mirror.ActionAndGuardCallOrder);
        });
    }

    private static void EquipmentWarmupPostfix() { }

    [Fact]
    public void PostNativeWeaponSwitch_IsCapturedWithAttackBeforeNextMovementPoll()
    {
        RunScenario(context =>
        {
            Agent owner = context.Spawn("peer", out MirrorAgent mirror, out _);
            mirror.Equipment[EquipmentIndex.Weapon0] = Weapon("old_weapon");
            mirror.Equipment[EquipmentIndex.Weapon1] = Weapon("new_weapon");
            mirror.PrimaryWieldedItemIndex = EquipmentIndex.Weapon0;
            context.Component.AgentActionHandler.PollActionsAfterNativeTick();
            context.Network.NetworkSentPackets.Packets.Clear();
            context.Component.AgentMovementHandler.PollMovement(0f);

            mirror.PrimaryWieldedItemIndex = EquipmentIndex.Weapon1;
            mirror.Action0Index = 1001;
            mirror.Action0CodeType = Agent.ActionCodeType.ReleaseMelee;
            context.Component.AgentActionHandler.PollActionsAfterNativeTick();

            AgentActionData data = Assert.Single(Assert.Single(
                context.Network.NetworkSentPackets.GetPackets<AgentActionPacket>()).Actions);
            Assert.Equal(1001, data.Action0Index);
            Assert.Equal((int)EquipmentIndex.Weapon1, data.Equipment.Value.MainHandIndex);
            Assert.Equal("new_weapon", data.Equipment.Value.MainHandItemId);
            Assert.Empty(context.Network.NetworkSentPackets.GetPackets<AgentEquipmentPacket>());
            context.Component.AgentActionHandler.PollActionsAfterNativeTick();
            Assert.Single(context.Network.NetworkSentPackets.GetPackets<AgentActionPacket>());
        });
    }

    [Fact]
    public void EquipmentChangeWithoutActionChange_StillPublishesCombinedSnapshot()
    {
        RunScenario(context =>
        {
            context.Spawn("peer", out MirrorAgent mirror, out _);
            mirror.Equipment[EquipmentIndex.Weapon0] = Weapon("spear", 2);
            mirror.PrimaryWieldedItemIndex = EquipmentIndex.Weapon0;
            context.Component.AgentActionHandler.PollActionsAfterNativeTick();
            context.Network.NetworkSentPackets.Packets.Clear();
            MissionWeapon weapon = mirror.Equipment[EquipmentIndex.Weapon0];
            weapon.CurrentUsageIndex = 1;
            mirror.Equipment[EquipmentIndex.Weapon0] = weapon;

            context.Component.AgentActionHandler.PollActionsAfterNativeTick();

            AgentActionData data = Assert.Single(Assert.Single(
                context.Network.NetworkSentPackets.GetPackets<AgentActionPacket>()).Actions);
            Assert.Equal(1, data.Equipment.Value.MainHandUsageIndex);
        });
    }

    [Fact]
    public void CombinedPacket_RoundTripsEquipmentIdentityAndUnwieldedOffhand()
    {
        RunScenario(context =>
        {
            Agent owner = context.Spawn("owner", out MirrorAgent mirror, out Guid id);
            mirror.Equipment[EquipmentIndex.Weapon1] = Weapon("spear", 2);
            MissionWeapon weapon = mirror.Equipment[EquipmentIndex.Weapon1];
            weapon.CurrentUsageIndex = 1;
            mirror.Equipment[EquipmentIndex.Weapon1] = weapon;
            mirror.PrimaryWieldedItemIndex = EquipmentIndex.Weapon1;
            var packet = Packet(owner, id, 2);
            var serializer = new ProtoBufSerializer(new SerializableTypeMapper());

            var result = Assert.IsType<AgentActionPacket>(
                serializer.Deserialize<IPacket>(serializer.Serialize(packet)));

            Assert.Equal(packet.Actions[0].Equipment, result.Actions[0].Equipment);
            Assert.Equal("spear", result.Actions[0].Equipment.Value.MainHandItemId);
            Assert.Equal(-1, result.Actions[0].Equipment.Value.OffHandIndex);
        });
    }

    [Fact]
    public void Receive_AppliesWeaponBeforeAttackAndIgnoresLegacyEquipmentAfterward()
    {
        RunScenario(context =>
        {
            Agent owner = context.Spawn("owner", out MirrorAgent ownerMirror, out _);
            Agent puppet = context.Spawn("owner", out MirrorAgent puppetMirror, out Guid id);
            ownerMirror.Equipment[EquipmentIndex.Weapon1] = Weapon("sword");
            puppetMirror.Equipment[EquipmentIndex.Weapon1] = ownerMirror.Equipment[EquipmentIndex.Weapon1];
            ownerMirror.PrimaryWieldedItemIndex = EquipmentIndex.Weapon1;
            ownerMirror.Action0Index = 1001;
            ownerMirror.Action0CodeType = Agent.ActionCodeType.ReleaseMelee;

            context.Receive(Packet(owner, id, 1));

            Assert.Equal(EquipmentIndex.Weapon1, puppet.GetPrimaryWieldedItemIndex());
            Assert.Equal(1001, puppetMirror.Action0Index);
            Assert.True(puppetMirror.ActionAndGuardCallOrder.IndexOf("wield")
                < puppetMirror.ActionAndGuardCallOrder.IndexOf("set-action"));
            context.Instance.Resolve<IAgentEquipmentApplier>().HandlePacket(null,
                new AgentEquipmentPacket(new[] { id }, new[]
                {
                    new AgentEquipmentData(EquipmentIndex.None, EquipmentIndex.None, 0)
                }));
            Drain();
            Assert.Equal(EquipmentIndex.Weapon1, puppet.GetPrimaryWieldedItemIndex());
        });
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void UnavailableWeapon_DefersActionUntilMatchingItemArrives(bool wrongItem)
    {
        RunScenario(context =>
        {
            Agent owner = context.Spawn("owner", out MirrorAgent ownerMirror, out _);
            context.Spawn("owner", out MirrorAgent puppetMirror, out Guid id);
            ownerMirror.Equipment[EquipmentIndex.Weapon1] = Weapon("sword");
            if (wrongItem) puppetMirror.Equipment[EquipmentIndex.Weapon1] = Weapon("bow");
            ownerMirror.PrimaryWieldedItemIndex = EquipmentIndex.Weapon1;
            ownerMirror.Action0Index = 1001;
            ownerMirror.Action0CodeType = Agent.ActionCodeType.ReleaseMelee;
            context.Receive(Packet(owner, id, 1));
            context.Component.AgentActionHandler.ApplyRemoteGuardStates();
            Assert.Equal(0, puppetMirror.SetActionChannelCalls);
            Assert.Equal(EquipmentIndex.None, puppetMirror.PrimaryWieldedItemIndex);

            puppetMirror.Equipment[EquipmentIndex.Weapon1] = ownerMirror.Equipment[EquipmentIndex.Weapon1];
            context.Component.AgentActionHandler.ApplyRemoteGuardStates();

            Assert.Equal(EquipmentIndex.Weapon1, puppetMirror.PrimaryWieldedItemIndex);
            Assert.Equal(1001, puppetMirror.Action0Index);
            int calls = puppetMirror.SetActionChannelCalls;
            context.Component.AgentActionHandler.ApplyRemoteGuardStates();
            Assert.Equal(calls, puppetMirror.SetActionChannelCalls);
        });
    }

    [Fact]
    public void NewerSnapshot_SupersedesPendingWeaponDependencyAndRejectsStaleReplay()
    {
        RunScenario(context =>
        {
            Agent owner = context.Spawn("owner", out MirrorAgent ownerMirror, out _);
            context.Spawn("owner", out MirrorAgent puppetMirror, out Guid id);
            ownerMirror.Equipment[EquipmentIndex.Weapon1] = Weapon("missing_sword");
            ownerMirror.PrimaryWieldedItemIndex = EquipmentIndex.Weapon1;
            ownerMirror.Action0Index = 1001;
            ownerMirror.Action0CodeType = Agent.ActionCodeType.ReleaseMelee;
            AgentActionPacket stale = Packet(owner, id, 1);
            context.Receive(stale);
            Assert.Equal(0, puppetMirror.SetActionChannelCalls);

            ownerMirror.PrimaryWieldedItemIndex = EquipmentIndex.None;
            ownerMirror.Action0Index = 1002;
            context.Receive(Packet(owner, id, 2));
            Assert.Equal(1002, puppetMirror.Action0Index);
            puppetMirror.Equipment[EquipmentIndex.Weapon1] = ownerMirror.Equipment[EquipmentIndex.Weapon1];
            context.Receive(stale);
            context.Component.AgentActionHandler.ApplyRemoteGuardStates();
            Assert.Equal(1002, puppetMirror.Action0Index);
            Assert.Equal(EquipmentIndex.None, puppetMirror.PrimaryWieldedItemIndex);
        });
    }

    [Fact]
    public void WrongAuthority_DoesNotApplyEquipmentOrAction()
    {
        RunScenario(context =>
        {
            Agent owner = context.Spawn("owner", out MirrorAgent ownerMirror, out _);
            context.Spawn("actual-owner", out MirrorAgent puppetMirror, out Guid id);
            ownerMirror.Equipment[EquipmentIndex.Weapon1] = Weapon("sword");
            puppetMirror.Equipment[EquipmentIndex.Weapon1] = ownerMirror.Equipment[EquipmentIndex.Weapon1];
            ownerMirror.PrimaryWieldedItemIndex = EquipmentIndex.Weapon1;
            context.Receive(Packet(owner, id, 1));
            context.Component.AgentActionHandler.ApplyRemoteGuardStates();
            Assert.Equal(EquipmentIndex.None, puppetMirror.PrimaryWieldedItemIndex);
            Assert.Equal(0, puppetMirror.SetActionChannelCalls);
        });
    }

    [Fact]
    public void UnchangedEquipment_UsesRevisionReferenceOnSubsequentActions()
    {
        RunScenario(context =>
        {
            context.Spawn("peer", out MirrorAgent mirror, out _);
            mirror.Equipment[EquipmentIndex.Weapon0] = Weapon("sword");
            mirror.PrimaryWieldedItemIndex = EquipmentIndex.Weapon0;
            context.Component.AgentActionHandler.PollActionsAfterNativeTick();
            AgentActionData baseline = Assert.Single(Assert.Single(
                context.Network.NetworkSentPackets.GetPackets<AgentActionPacket>()).Actions);
            Assert.NotNull(baseline.Equipment);
            Assert.True(baseline.EquipmentRevision > 0);
            context.Network.NetworkSentPackets.Packets.Clear();
            mirror.Action0Index = 1001;
            mirror.Action0CodeType = Agent.ActionCodeType.ReleaseMelee;
            context.Component.AgentActionHandler.PollActionsAfterNativeTick();
            AgentActionData reference = Assert.Single(Assert.Single(
                context.Network.NetworkSentPackets.GetPackets<AgentActionPacket>()).Actions);
            Assert.Null(reference.Equipment);
            Assert.Equal(baseline.EquipmentRevision, reference.EquipmentRevision);
        });
    }

    [Fact]
    public void NewerReference_RetainsMissingEquipmentBaselineWhileReplacingPendingAction()
    {
        RunScenario(context =>
        {
            Agent owner = context.Spawn("owner", out MirrorAgent ownerMirror, out _);
            context.Spawn("owner", out MirrorAgent puppet, out Guid id);
            ownerMirror.Equipment[EquipmentIndex.Weapon0] = Weapon("sword");
            ownerMirror.PrimaryWieldedItemIndex = EquipmentIndex.Weapon0;
            ownerMirror.Action0Index = 1001;
            context.Receive(RevisionPacket(owner, id, 1, 1, true));
            ownerMirror.Action0Index = 1002;
            context.Receive(RevisionPacket(owner, id, 2, 1, false));
            Assert.Equal(0, puppet.SetActionChannelCalls);

            puppet.Equipment[EquipmentIndex.Weapon0] = ownerMirror.Equipment[EquipmentIndex.Weapon0];
            context.Component.AgentActionHandler.ApplyRemoteGuardStates();
            Assert.Equal(1002, puppet.Action0Index);
            Assert.DoesNotContain(1001, puppet.SetActionChannelIndices);
            Assert.Equal(EquipmentIndex.Weapon0, puppet.PrimaryWieldedItemIndex);
        });
    }

    [Fact]
    public void BaselineBeforeRegistration_SurvivesNewerReference()
    {
        RunScenario(context =>
        {
            Agent owner = context.Spawn("owner", out MirrorAgent ownerMirror, out _);
            ownerMirror.Action0Index = 1001;
            Guid id = Guid.NewGuid();
            context.Receive(RevisionPacket(owner, id, 1, 1, true));
            ownerMirror.Action0Index = 1002;
            context.Receive(RevisionPacket(owner, id, 2, 1, false));
            Agent puppet = context.Mission.SpawnAgent(new AgentBuildData(Game.Current.PlayerTroop)
                .Controller(AgentControllerType.None));
            Assert.True(AgentMirror.TryGet(puppet, out MirrorAgent mirror));
            Assert.True(context.Registry.TryRegisterAgent("owner", id, puppet));
            context.Component.AgentActionHandler.ApplyRemoteGuardStates();
            Assert.Equal(1002, mirror.Action0Index);
        });
    }

    [Fact]
    public void UnknownRevision_WaitsForMatchingBaseline()
    {
        RunScenario(context =>
        {
            Agent owner = context.Spawn("owner", out MirrorAgent ownerMirror, out _);
            context.Spawn("owner", out MirrorAgent puppet, out Guid id);
            ownerMirror.Action0Index = 1001;
            context.Receive(RevisionPacket(owner, id, 1, 1, true));
            ownerMirror.Action0Index = 1002;
            context.Receive(RevisionPacket(owner, id, 2, 2, false));
            Assert.Equal(1001, puppet.Action0Index);
            context.Receive(RevisionPacket(owner, id, 3, 2, true));
            Assert.Equal(1002, puppet.Action0Index);
        });
    }

    [Fact]
    public void NewAuthority_CannotResolveReferenceAgainstPreviousOwnersBaseline()
    {
        RunScenario(context =>
        {
            Agent owner = context.Spawn("owner", out MirrorAgent ownerMirror, out _);
            context.Spawn("owner", out MirrorAgent puppet, out Guid id);
            ownerMirror.Action0Index = 1001;
            context.Receive(RevisionPacket(owner, id, 1, 1, true));
            Assert.True(context.Registry.TryTransferAuthority("next-owner", id));
            ownerMirror.Action0Index = 1002;
            context.Receive(RevisionPacket(owner, id, 2, 1, false, "next-owner", authorityRevision: 1));
            Assert.Equal(1001, puppet.Action0Index);
            context.Receive(RevisionPacket(owner, id, 3, 1, true, "next-owner", authorityRevision: 1));
            Assert.Equal(1002, puppet.Action0Index);
        });
    }

    [Fact]
    public void ChangedAuthorityRevision_PublishesFreshBaselineWithoutActionChange()
    {
        RunScenario(context =>
        {
            context.Spawn("peer", out MirrorAgent mirror, out Guid id);
            mirror.Equipment[EquipmentIndex.Weapon0] = Weapon("sword");
            mirror.PrimaryWieldedItemIndex = EquipmentIndex.Weapon0;
            context.Component.AgentActionHandler.PollActionsAfterNativeTick();
            AgentActionData initial = Assert.Single(Assert.Single(
                context.Network.NetworkSentPackets.GetPackets<AgentActionPacket>()).Actions);
            context.Network.NetworkSentPackets.Packets.Clear();
            Assert.True(context.Registry.TryTransferAuthority("peer", id, 1));
            context.Component.AgentActionHandler.PollActionsAfterNativeTick();
            AgentActionData refreshed = Assert.Single(Assert.Single(
                context.Network.NetworkSentPackets.GetPackets<AgentActionPacket>()).Actions);
            Assert.NotNull(refreshed.Equipment);
            Assert.True(refreshed.EquipmentRevision > initial.EquipmentRevision);
        });
    }

    [Fact]
    public void CatchUpChangedEquipment_DoesNotConsumeBaselineNeededByExistingPeers()
    {
        RunScenario(context =>
        {
            context.Spawn("peer", out MirrorAgent mirror, out _);
            mirror.Equipment[EquipmentIndex.Weapon0] = Weapon("sword");
            mirror.Equipment[EquipmentIndex.Weapon1] = Weapon("axe");
            mirror.PrimaryWieldedItemIndex = EquipmentIndex.Weapon0;
            context.Component.AgentActionHandler.PollActionsAfterNativeTick();
            context.Network.NetworkSentPackets.Packets.Clear();
            mirror.PrimaryWieldedItemIndex = EquipmentIndex.Weapon1;
            context.Component.AgentActionHandler.CatchUpJoiner("joiner");
            Drain();
            AgentActionData catchUp = Assert.Single(Assert.IsType<AgentActionPacket>(
                Assert.Single(context.Network.DirectPacketSends).Packet).Actions);
            context.Network.NetworkSentPackets.Packets.Clear();
            context.Component.AgentActionHandler.PollActionsAfterNativeTick();
            AgentActionData broadcast = Assert.Single(Assert.Single(
                context.Network.NetworkSentPackets.GetPackets<AgentActionPacket>()).Actions);
            Assert.NotNull(catchUp.Equipment);
            Assert.NotNull(broadcast.Equipment);
            Assert.Equal(catchUp.EquipmentRevision, broadcast.EquipmentRevision);
        });
    }

    [Fact]
    public void CatchUpUnarmedAgent_SendsBaselineForLaterReferences()
    {
        RunScenario(context =>
        {
            context.Spawn("peer", out MirrorAgent mirror, out _);
            mirror.Action0Index = 1001;
            mirror.Action0CodeType = Agent.ActionCodeType.ReleaseMelee;
            context.Component.AgentActionHandler.PollActionsAfterNativeTick();
            context.Network.NetworkSentPackets.Packets.Clear();
            context.Component.AgentActionHandler.CatchUpJoiner("joiner");
            Drain();
            AgentActionData baseline = Assert.Single(Assert.IsType<AgentActionPacket>(
                Assert.Single(context.Network.DirectPacketSends).Packet).Actions);
            Assert.NotNull(baseline.Equipment);
            context.Network.NetworkSentPackets.Packets.Clear();
            mirror.Action0Index = 1002;
            context.Component.AgentActionHandler.PollActionsAfterNativeTick();
            AgentActionData reference = Assert.Single(Assert.Single(
                context.Network.NetworkSentPackets.GetPackets<AgentActionPacket>()).Actions);
            Assert.Null(reference.Equipment);
            Assert.Equal(baseline.EquipmentRevision, reference.EquipmentRevision);
        });
    }

    [Fact]
    public void RevisionReference_RoundTripsWithoutFullEquipmentAndAddsThreeBytes()
    {
        RunScenario(context =>
        {
            Agent owner = context.Spawn("owner", out _, out Guid id);
            var serializer = new ProtoBufSerializer(new SerializableTypeMapper());
            AgentActionPacket reference = RevisionPacket(owner, id, 1, 1, false);
            var legacy = new AgentActionPacket("owner", new[] { id },
                new[] { new AgentActionData(owner).WithEquipment(0, null) }, new[] { 1L });
            byte[] wire = serializer.Serialize(reference);
            var roundTrip = Assert.IsType<AgentActionPacket>(serializer.Deserialize<IPacket>(wire));
            Assert.Null(roundTrip.Actions[0].Equipment);
            Assert.Equal(1L, roundTrip.Actions[0].EquipmentRevision);
            Assert.Equal(3, wire.Length - serializer.Serialize(legacy).Length);
        });
    }

    private static AgentActionPacket RevisionPacket(Agent owner, Guid id, long sequence,
        long revision, bool includeEquipment, string controller = "owner", int epoch = 0, long authorityRevision = 0)
    {
        var data = new AgentActionData(owner);
        return new AgentActionPacket(controller, new[] { id },
            new[] { data.WithEquipment(revision, includeEquipment ? data.Equipment : null, authorityRevision) },
            new[] { sequence }, epoch);
    }

    [Theory]
    [InlineData(false, false, false, false)]
    [InlineData(false, true, false, false)]
    [InlineData(true, false, false, false)]
    [InlineData(true, true, false, false)]
    [InlineData(false, false, true, false)]
    [InlineData(false, true, true, false)]
    [InlineData(false, false, false, true)]
    [InlineData(false, true, false, true)]
    [InlineData(false, false, true, true)]
    [InlineData(false, true, true, true)]
    public void FormerHost_PendingOrdinaryActionReplaysWhenEquipmentArrives(
        bool regainAuthority, bool latestIsReference, bool delayRegistration, bool delayAssignment)
    {
        RunScenario(context =>
        {
            const string mapEventId = "mapEvent1";
            BattleSpawnGate.BeginBattle(mapEventId);
            try
            {
                var broker = context.Instance.Resolve<IMessageBroker>();
                var hosts = context.Instance.Resolve<IBattleHostRegistry>();
                broker.Publish(this, new NetworkMissionPeerEntered("A", mapEventId));
                broker.Publish(this, new NetworkMissionPeerEntered("B", mapEventId));

                void AssignHost(string controller, int epoch)
                {
                    hosts.Set(mapEventId, new BattleHostAssignment(controller, Array.Empty<string>(), epoch));
                    broker.Publish(this, new NetworkBattleHostAssigned(
                        mapEventId, controller, Array.Empty<string>(), epoch));
                    Drain();
                }

                void ReceiveWithoutSweep(AgentActionPacket packet)
                {
                    context.Component.AgentActionHandler.HandlePacket(null, packet);
                    Drain();
                }

                AssignHost("A", 1);
                Agent owner = context.Spawn("A", out MirrorAgent ownerMirror, out _);
                Guid id = Guid.NewGuid();
                Agent puppetAgent = context.Mission.SpawnAgent(new AgentBuildData(Game.Current.PlayerTroop)
                    .Controller(AgentControllerType.None));
                Assert.True(AgentMirror.TryGet(puppetAgent, out MirrorAgent puppet));
                if (!delayRegistration)
                    Assert.True(context.Registry.TryRegisterAgent("A", id, puppetAgent));
                ownerMirror.Equipment[EquipmentIndex.Weapon0] = Weapon("sword");
                ownerMirror.PrimaryWieldedItemIndex = EquipmentIndex.Weapon0;
                ownerMirror.Action0Index = 1001;
                ownerMirror.Action0CodeType = Agent.ActionCodeType.ReleaseMelee;
                ReceiveWithoutSweep(RevisionPacket(owner, id, 1, 1, true, "A", 1));
                Assert.Equal(0, puppet.SetActionChannelCalls);

                if (!delayAssignment) AssignHost("B", 2);
                if (regainAuthority)
                {
                    Assert.True(context.Registry.TryTransferAuthority("B", id));
                    Assert.True(context.Registry.TryTransferAuthority("A", id));
                }
                ownerMirror.Action0Index = 1002;
                ReceiveWithoutSweep(RevisionPacket(owner, id, 2, 2, true, "A", 0, regainAuthority ? 2 : 0));
                ownerMirror.Action0Index = 1003;
                ReceiveWithoutSweep(RevisionPacket(owner, id, 3, 2, !latestIsReference, "A", 0, regainAuthority ? 2 : 0));
                if (delayRegistration)
                    Assert.True(context.Registry.TryRegisterAgent("A", id, puppetAgent));
                if (delayAssignment) AssignHost("B", 2);
                ownerMirror.Action0Index = 1004;
                ReceiveWithoutSweep(RevisionPacket(owner, id, 4, 99, true, "A", 1));
                context.Component.AgentActionHandler.ApplyRemoteGuardStates();
                Assert.Equal(0, puppet.SetActionChannelCalls);

                puppet.Equipment[EquipmentIndex.Weapon0] = ownerMirror.Equipment[EquipmentIndex.Weapon0];
                context.Component.AgentActionHandler.ApplyRemoteGuardStates();
                Assert.Equal(1003, puppet.Action0Index);
                Assert.Equal(EquipmentIndex.Weapon0, puppet.PrimaryWieldedItemIndex);
                int calls = puppet.SetActionChannelCalls;
                context.Component.AgentActionHandler.ApplyRemoteGuardStates();
                Assert.Equal(calls, puppet.SetActionChannelCalls);
            }
            finally
            {
                BattleSpawnGate.EndBattle();
            }
        });
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public void ReturningSender_FreshRevisionAppliesOnIndependentObserver(bool highOldRevision, bool delayGrant)
    {
        using var fixture = new MissionEngineFixture();
        var clients = Clients.ToArray();
        string[] owners = { "A", "B", "C" };
        var id = Guid.NewGuid();
        var mirrors = new MirrorAgent[3];
        var handlers = new IAgentActionHandler[3];
        for (int i = 0; i < clients.Length; i++)
        {
            int index = i;
            SetControllerId(clients[i], owners[i]);
            clients[i].Call(() =>
            {
                var mission = fixture.CreateMission(clients[index]);
                Agent agent = mission.SpawnAgent(new AgentBuildData(Game.Current.PlayerTroop)
                    .Controller(index == 0 ? AgentControllerType.AI : AgentControllerType.None));
                Assert.True(AgentMirror.TryGet(agent, out mirrors[index]));
                Assert.True(clients[index].Resolve<INetworkAgentRegistry>().TryRegisterAgent("A", id, agent));
                mirrors[index].Equipment[EquipmentIndex.Weapon0] = Weapon("sword");
                mirrors[index].Equipment[EquipmentIndex.Weapon1] = Weapon("axe");
                mirrors[index].PrimaryWieldedItemIndex = EquipmentIndex.Weapon0;
                mirrors[index].Action0Index = 1001;
                mirrors[index].Action0CodeType = Agent.ActionCodeType.ReleaseMelee;
                handlers[index] = clients[index].Resolve<ICoopMissionComponent>().AgentActionHandler;
            });
        }
        AgentActionPacket Send(int index, int action, EquipmentIndex slot)
        {
            AgentActionPacket packet = default;
            clients[index].Call(() =>
            {
                var network = clients[index].Resolve<MockBattleNetwork>();
                network.NetworkSentPackets.Packets.Clear();
                mirrors[index].PrimaryWieldedItemIndex = slot;
                mirrors[index].Action0Index = action;
                handlers[index].PollActionsAfterNativeTick();
                var serializer = new ProtoBufSerializer(new SerializableTypeMapper());
                packet = Assert.IsType<AgentActionPacket>(serializer.Deserialize<IPacket>(serializer.Serialize(
                    Assert.Single(network.NetworkSentPackets.GetPackets<AgentActionPacket>()))));
            });
            return packet;
        }
        void Receive(AgentActionPacket packet)
        {
            clients[2].Call(() =>
            {
                handlers[2].HandlePacket(null, packet);
                Drain();
                handlers[2].ApplyRemoteGuardStates();
            });
        }
        void Transfer(int index, string owner)
        {
            clients[index].Call(() => Assert.True(
                clients[index].Resolve<INetworkAgentRegistry>().TryTransferAuthority(owner, id)));
        }
        AgentActionPacket old = Send(0, 1001, EquipmentIndex.Weapon0);
        Receive(old);
        if (highOldRevision)
        {
            Receive(Send(0, 1002, EquipmentIndex.Weapon1));
            old = Send(0, 1003, EquipmentIndex.Weapon0);
            Receive(old);
        }
        AgentActionPacket oldReference = Send(0, 1004, EquipmentIndex.Weapon0);
        Receive(oldReference);
        Assert.Equal(highOldRevision ? 3 : 1, old.Actions[0].EquipmentRevision);
        Assert.Null(oldReference.Actions[0].Equipment);
        for (int i = 0; i < 3; i++) Transfer(i, "B");
        Receive(Send(1, 1005, EquipmentIndex.Weapon0));
        Assert.Equal(1005, mirrors[2].Action0Index);
        Transfer(0, "A");
        Transfer(1, "A");
        clients[0].Call(() =>
        {
            handlers[0].Dispose();
            handlers[0] = clients[0].Resolve<IAgentActionHandler>();
        });
        if (!delayGrant) Transfer(2, "A");
        var fresh = Send(0, 1006, EquipmentIndex.Weapon1);
        Assert.Equal(1, fresh.Actions[0].EquipmentRevision);
        Assert.Equal(2, fresh.Actions[0].AuthorityRevision);
        Assert.Equal(1, fresh.Sequences[0]);
        Receive(fresh);
        Receive(old);
        Receive(oldReference);
        if (delayGrant)
        {
            Assert.Equal(1005, mirrors[2].Action0Index);
            Transfer(2, "A");
            clients[2].Call(() => handlers[2].ApplyRemoteGuardStates());
        }
        Assert.Equal(1006, mirrors[2].Action0Index);
        Assert.Equal(EquipmentIndex.Weapon1, mirrors[2].PrimaryWieldedItemIndex);
        int calls = mirrors[2].SetActionChannelCalls;
        Receive(fresh);
        Assert.Equal(calls, mirrors[2].SetActionChannelCalls);
        var reference = Send(0, 1007, EquipmentIndex.Weapon1);
        Assert.Null(reference.Actions[0].Equipment);
        Assert.Equal(2, reference.Actions[0].AuthorityRevision);
        Receive(reference);
        Assert.Equal(1007, mirrors[2].Action0Index);
        calls = mirrors[2].SetActionChannelCalls;
        Receive(old);
        Receive(oldReference);
        Receive(reference);
        Assert.Equal(calls, mirrors[2].SetActionChannelCalls);
        Assert.Equal(EquipmentIndex.Weapon1, mirrors[2].PrimaryWieldedItemIndex);
        clients[0].Call(() => handlers[0].Dispose());
    }

    [Fact]
    public void CatchUpAfterHostEpochChange_PublishesRefreshToExistingPeersWithHeldGuard()
    {
        RunScenario(context =>
        {
            const string battleId = "equipment-epoch";
            BattleSpawnGate.BeginBattle(battleId);
            try
            {
                var hosts = context.Instance.Resolve<IBattleHostRegistry>();
                hosts.Set(battleId, new BattleHostAssignment("peer", Array.Empty<string>(), 1));
                context.Spawn("peer", out var mirror, out _);
                mirror.Equipment[EquipmentIndex.Weapon0] = Weapon("sword");
                mirror.PrimaryWieldedItemIndex = EquipmentIndex.Weapon0;
                mirror.MovementFlags = Agent.MovementControlFlag.DefendLeft | Agent.MovementControlFlag.DefendBlock;
                context.Component.AgentActionHandler.PollActionsAfterNativeTick();
                context.Network.NetworkSentPackets.Packets.Clear();
                hosts.Set(battleId, new BattleHostAssignment("peer", Array.Empty<string>(), 2));
                context.Component.AgentActionHandler.CatchUpJoiner("joiner");
                Drain();
                var catchUp = Assert.IsType<AgentActionPacket>(Assert.Single(context.Network.DirectPacketSends).Packet);
                context.Network.NetworkSentPackets.Packets.Clear();
                context.Component.AgentActionHandler.PollActionsAfterNativeTick();
                var refresh = Assert.Single(context.Network.NetworkSentPackets.GetPackets<AgentActionPacket>());
                Assert.Equal(2, refresh.BattleHostEpoch);
                Assert.NotNull(refresh.Actions[0].Equipment);
                Assert.Equal(catchUp.Actions[0].EquipmentRevision, refresh.Actions[0].EquipmentRevision);
                Assert.Equal(catchUp.Actions[0].DefendFlags, refresh.Actions[0].DefendFlags);
                Assert.NotEqual(Agent.MovementControlFlag.None, refresh.Actions[0].DefendFlags);
                context.Component.AgentActionHandler.PollActionsAfterNativeTick();
                Assert.Single(context.Network.NetworkSentPackets.GetPackets<AgentActionPacket>());
            }
            finally { BattleSpawnGate.EndBattle(); }
        });
    }

    private static MissionWeapon Weapon(string itemId, int usages = 1)
    {
        using var allowed = new AllowedThread();
        var item = new ItemObject { StringId = itemId };
        Assert.Equal(itemId, item.StringId);
        var weapon = new MissionWeapon(item, null, null);
        var weapons = new List<WeaponComponentData>();
        for (int i = 0; i < usages; i++)
            weapons.Add(new WeaponComponentData(null, WeaponClass.OneHandedSword, default));
        object boxed = weapon;
        typeof(MissionWeapon).GetField("_weapons", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)
            .SetValue(boxed, weapons);
        return (MissionWeapon)boxed;
    }

    private static AgentActionPacket Packet(Agent owner, Guid id, long sequence)
    {
        return new AgentActionPacket("owner", new[] { id },
            new[] { new AgentActionData(owner).WithEquipment(sequence, new AgentEquipmentData(owner)) }, new[] { sequence });
    }

    private static void Drain() => GameThread.Run(() => { }, blocking: true);

    private void RunScenario(Action<Context> scenario)
    {
        using var fixture = new MissionEngineFixture();
        EnvironmentInstance instance = Clients.First();
        SetControllerId(instance, "peer");
        instance.Call(() => scenario(new Context(fixture, instance)));
    }

    private sealed class Context
    {
        public EnvironmentInstance Instance { get; }
        public MockMission Mission { get; }
        public ICoopMissionComponent Component { get; }
        public INetworkAgentRegistry Registry { get; }
        public MockBattleNetwork Network { get; }

        public Context(MissionEngineFixture fixture, EnvironmentInstance instance)
        {
            Instance = instance;
            Mission = fixture.CreateMission(instance);
            Component = instance.Resolve<ICoopMissionComponent>();
            Registry = instance.Resolve<INetworkAgentRegistry>();
            Network = instance.Resolve<MockBattleNetwork>();
        }

        public Agent Spawn(string owner, out MirrorAgent mirror, out Guid id)
        {
            Agent agent = Mission.SpawnAgent(new AgentBuildData(Game.Current.PlayerTroop)
                .Controller(owner == "peer" ? AgentControllerType.AI : AgentControllerType.None));
            Assert.True(AgentMirror.TryGet(agent, out mirror));
            id = Guid.NewGuid();
            Assert.True(Registry.TryRegisterAgent(owner, id, agent));
            return agent;
        }

        public void Receive(AgentActionPacket packet)
        {
            Component.AgentActionHandler.HandlePacket(null, packet);
            Drain();
            Component.AgentActionHandler.ApplyRemoteGuardStates();
        }
    }
}
