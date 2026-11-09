using Common.Messaging;
using Common.PacketHandlers;
using Common.Serialization;
using Common.Util;
using E2E.Tests.Environment.Instance;
using E2E.Tests.Environment.Mock;
using E2E.Tests.Environment.MockEngine;
using GameInterface.Services.ObjectManager;
using GameInterface.Surrogates;
using HarmonyLib;
using Missions;
using Missions.Agents;
using Missions.Agents.Handlers;
using Missions.Agents.Messages;
using Missions.Agents.Packets;
using Missions.Battles;
using Missions.Missiles.Handlers;
using Missions.Missiles.Message;
using Missions.Missiles.Patches;
using Moq;
using System.Reflection;
using TaleWorlds.Core;
using TaleWorlds.Engine;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
using TaleWorlds.ObjectSystem;
using Xunit.Abstractions;

namespace E2E.Tests.Services.Missions;

[Collection(nameof(Tournaments.TournamentCombatInfrastructureCollection))]
public partial class MissileOutcomeTests : MissionTestEnvironment
{
    private readonly ITestOutputHelper output;

    public MissileOutcomeTests(ITestOutputHelper output) : base(output)
    {
        this.output = output;
        new SurrogateCollection();
    }

    [Theory]
    [InlineData(MissileOutcomeKind.Ground, false)]
    [InlineData(MissileOutcomeKind.Ground, true)]
    [InlineData(MissileOutcomeKind.BounceSettled, false)]
    [InlineData(MissileOutcomeKind.BounceSettled, true)]
    [InlineData(MissileOutcomeKind.Body, false)]
    [InlineData(MissileOutcomeKind.Body, true)]
    [InlineData(MissileOutcomeKind.Shield, false)]
    [InlineData(MissileOutcomeKind.Shield, true)]
    public void VisibleOutcome_ReplacesPeerCollisionInEitherOrder(MissileOutcomeKind kind, bool contactFirst)
    {
        RunClients((shooter, peer, native) =>
        {
            NetworkAgentShoot shot = shooter.Shoot();
            peer.Receive(shot);
            Mission.Missile? replay = null;
            peer.Instance.Call(() =>
            {
                peer.Handler.DrainPendingShots();
                replay = Assert.Single(peer.Mission.Shell._missilesDictionary).Value;
                if (contactFirst) peer.Contact(replay, Mission.MissileCollisionReaction.Stick);
                if (kind == MissileOutcomeKind.Shield) native.OffhandSlots[peer.Victim] = EquipmentIndex.Weapon2;
                Assert.Empty(peer.Items.GetAll());
            });

            NetworkMissileOutcome? sent = null;
            MatrixFrame expected = Frame(kind is MissileOutcomeKind.Body or MissileOutcomeKind.Shield);
            shooter.Instance.Call(() =>
            {
                Mission.Missile missile = Assert.Single(shooter.Mission.Shell._missilesDictionary).Value;
                bool attached = kind is MissileOutcomeKind.Body or MissileOutcomeKind.Shield;
                shooter.Contact(missile, kind == MissileOutcomeKind.BounceSettled
                    ? Mission.MissileCollisionReaction.BounceBack : Mission.MissileCollisionReaction.Stick,
                    kind == MissileOutcomeKind.BounceSettled ? MatrixFrame.Identity : expected,
                    attached ? shooter.Victim : null, kind == MissileOutcomeKind.Shield);
                if (kind == MissileOutcomeKind.BounceSettled)
                {
                    Assert.Empty(shooter.Network.NetworkSentMessages.GetMessages<NetworkMissileOutcome>());
                    SpawnedItemEntity item = Assert.Single(shooter.Items.GetAll()).Value;
                    shooter.Spawner.Frames[item] = expected;
                    item.DisableDynamicBody();
                    item.DisableDynamicBody();
                }
                sent = Assert.Single(shooter.Network.NetworkSentMessages.GetMessages<NetworkMissileOutcome>());
                Assert.Equal(kind, sent.Kind);
            });
            peer.Receive(sent!);
            peer.Instance.Call(() =>
            {
                Assert.Empty(peer.Mission.Shell._missilesDictionary);
                Assert.Contains(replay!.Entity, native.RemovedEntities);
                Assert.Empty(peer.Network.NetworkSentMessages.GetMessages<NetworkMissileOutcome>());
                if (kind is MissileOutcomeKind.Body or MissileOutcomeKind.Shield)
                {
                    Assert.Empty(peer.Items.GetAll());
                    NativeFixture.Attachment attached = Assert.Single(native.Attachments,
                        attachment => ReferenceEquals(attachment.Victim, peer.Victim));
                    Assert.Equal(kind == MissileOutcomeKind.Shield, attached.Shield);
                    Assert.Equal(kind == MissileOutcomeKind.Shield ? (int)EquipmentIndex.Weapon2 : 7, attached.Index);
                    AssertFrame(expected, attached.Frame, exact: true);
                    Assert.Equal(9, sent!.Rotation.Length);
                }
                else
                {
                    var item = Assert.Single(peer.Items.GetAll());
                    Assert.Equal(sent!.WorldItemId, item.Key);
                    Assert.Equal(MissileHandler.CreateMissileItemId(shot.AgentId, shot.ShotSequence), item.Key);
                    Assert.True(item.Value.IsStuckMissile());
                    AssertFrame(expected, peer.Spawner.Frames[item.Value], exact: false);
                    Assert.Equal(kind == MissileOutcomeKind.BounceSettled, item.Value.PhysicsStopped);
                    Assert.Equal(kind == MissileOutcomeKind.BounceSettled ? 1 : 0, native.SnapCalls);
                    Assert.Equal(4, sent.Rotation.Length);
                    Assert.Equal(1, peer.Spawner.SpawnCount);
                }
            });
        });
    }

    [Fact]
    public void OutcomeBeforeReconstruction_SpawnsWithoutCreatingAFlyingCopy()
    {
        RunClients((shooter, peer, native) =>
        {
            NetworkAgentShoot shot = shooter.Shoot();
            NetworkMissileOutcome? outcome = null;
            shooter.Instance.Call(() =>
            {
                shooter.Contact(Assert.Single(shooter.Mission.Shell._missilesDictionary).Value,
                    Mission.MissileCollisionReaction.Stick, Frame(false));
                outcome = Assert.Single(shooter.Network.NetworkSentMessages.GetMessages<NetworkMissileOutcome>());
            });
            peer.Receive(outcome!);
            peer.Receive(shot);
            peer.Instance.Call(() =>
            {
                peer.Handler.DrainPendingShots();
                Assert.Empty(peer.Mission.Shell._missilesDictionary);
                Assert.Single(peer.Items.GetAll());
                Assert.Equal(0, native.ReconstructedMissiles);
            });
        });
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void LethalHit_AttachesAfterBothVictimsWereDeregistered(bool shield)
    {
        RunClients((shooter, peer, native) =>
        {
            peer.Receive(shooter.Shoot());
            peer.Instance.Call(() =>
            {
                peer.Handler.DrainPendingShots();
                using var deaths = new AgentDeathHandler(peer.Instance.Resolve<INetworkAgentRegistry>(), peer.Broker);
                peer.Broker.Publish(this, new AgentDied(peer.Victim));
                Assert.False(peer.Instance.Resolve<INetworkAgentRegistry>().TryGetAgentInfo(peer.VictimId, out _));
            });
            NetworkMissileOutcome? outcome = null;
            shooter.Instance.Call(() =>
            {
                var agents = shooter.Instance.Resolve<INetworkAgentRegistry>();
                Assert.True(agents.TryTransferAuthority("shooter", shooter.VictimId));
                using var deaths = new AgentDeathHandler(agents, shooter.Broker);
                shooter.Broker.Publish(this, new AgentDied(shooter.Victim));
                Assert.False(agents.TryGetAgentInfo(shooter.VictimId, out _));
                shooter.Contact(Assert.Single(shooter.Mission.Shell._missilesDictionary).Value,
                    Mission.MissileCollisionReaction.Stick, Frame(true), shooter.Victim, shield);
                outcome = Assert.Single(shooter.Network.NetworkSentMessages.GetMessages<NetworkMissileOutcome>());
            });
            peer.Receive(outcome!);
            peer.Instance.Call(() =>
            {
                var attachment = Assert.Single(native.Attachments, value => ReferenceEquals(value.Victim, peer.Victim));
                Assert.Equal(shield, attachment.Shield);
                AssertFrame(Frame(true), attachment.Frame, exact: true);
                Assert.Empty(peer.Mission.Shell._missilesDictionary);
                peer.Mission.Shell.GetMissionBehavior<CoopMissionController>().OnAgentDeleted(peer.Victim);
                var registry = peer.Instance.Resolve<INetworkAgentRegistry>();
                Assert.False(registry.TryGetCosmeticAgent(peer.VictimId, out _));
                Assert.False(registry.TryGetCosmeticAgentId(peer.Victim, out _));
            });
        });
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void MissileLandingAfterShooterDeath_StillRegistersTheCanonicalWorldItem(bool nativeDeleted)
    {
        RunClients((shooter, peer, native) =>
        {
            NetworkAgentShoot shot = shooter.Shoot();
            peer.Receive(shot);
            NetworkMissileOutcome? outcome = null;
            Guid itemId = MissileHandler.CreateMissileItemId(shot.AgentId, shot.ShotSequence);
            shooter.Instance.Call(() =>
            {
                using var deaths = new AgentDeathHandler(shooter.Instance.Resolve<INetworkAgentRegistry>(), shooter.Broker);
                shooter.Broker.Publish(this, new AgentDied(shooter.Shooter));
                if (nativeDeleted)
                    shooter.Mission.Shell.GetMissionBehavior<CoopMissionController>().OnAgentDeleted(shooter.Shooter);
                shooter.Contact(Assert.Single(shooter.Mission.Shell._missilesDictionary).Value,
                    Mission.MissileCollisionReaction.Stick, Frame(false));
                Assert.True(shooter.Items.TryGet(itemId, out var item));
                Assert.True(item.IsStuckMissile());
                outcome = Assert.Single(shooter.Network.NetworkSentMessages.GetMessages<NetworkMissileOutcome>());
            });
            peer.Receive(outcome!);
            peer.Instance.Call(() =>
            {
                peer.Handler.DrainPendingShots();
                Assert.True(peer.Items.TryGet(itemId, out _));
            });
        });
    }

    [Fact]
    public void GroundOutcome_UsesTheFinalGlobalItemFrame()
    {
        RunClients((shooter, peer, native) =>
        {
            peer.Receive(shooter.Shoot());
            NetworkMissileOutcome? outcome = null;
            native.DropFrameOverride = Frame(false);
            shooter.Instance.Call(() =>
            {
                shooter.Contact(Assert.Single(shooter.Mission.Shell._missilesDictionary).Value,
                    Mission.MissileCollisionReaction.Stick, MatrixFrame.Identity);
                outcome = Assert.Single(shooter.Network.NetworkSentMessages.GetMessages<NetworkMissileOutcome>());
                Assert.Equal(Frame(false).origin, outcome.Origin);
            });
            peer.Receive(outcome!);
            peer.Instance.Call(() =>
            {
                peer.Handler.DrainPendingShots();
                var item = Assert.Single(peer.Items.GetAll());
                AssertFrame(Frame(false), peer.Spawner.Frames[item.Value], exact: false);
            });
        });
    }

    [Fact]
    public void OutcomeWhileSceneIsLoading_WaitsForTheSpawner()
    {
        RunClients((shooter, peer, native) =>
        {
            peer.Receive(shooter.Shoot());
            peer.Instance.Call(() => peer.Handler.DrainPendingShots());
            NetworkMissileOutcome? outcome = null;
            shooter.Instance.Call(() =>
            {
                shooter.Contact(Assert.Single(shooter.Mission.Shell._missilesDictionary).Value,
                    Mission.MissileCollisionReaction.Stick, Frame(false));
                outcome = Assert.Single(shooter.Network.NetworkSentMessages.GetMessages<NetworkMissileOutcome>());
            });
            peer.Spawner.IsReady = false;
            peer.Receive(outcome!);
            peer.Instance.Call(() =>
            {
                peer.Handler.DrainPendingShots();
                Assert.Empty(peer.Items.GetAll());
                peer.Spawner.IsReady = true;
                peer.Handler.DrainPendingShots();
                Assert.Single(peer.Items.GetAll());
                Assert.Empty(peer.Mission.Shell._missilesDictionary);
            });
        });
    }

    [Fact]
    public void InvisibleOutcome_SendsNothingAndPeerContactLeavesNothing()
    {
        RunClients((shooter, peer, native) =>
        {
            peer.Receive(shooter.Shoot());
            shooter.Instance.Call(() =>
            {
                shooter.Contact(Assert.Single(shooter.Mission.Shell._missilesDictionary).Value,
                    Mission.MissileCollisionReaction.BecomeInvisible);
                Assert.Empty(shooter.Network.NetworkSentMessages.GetMessages<NetworkMissileOutcome>());
                Assert.Empty(shooter.Items.GetAll());
            });
            peer.Instance.Call(() =>
            {
                peer.Handler.DrainPendingShots();
                peer.Contact(Assert.Single(peer.Mission.Shell._missilesDictionary).Value,
                    Mission.MissileCollisionReaction.BounceBack);
                Assert.Empty(peer.Items.GetAll());
                Assert.Empty(peer.Mission.Shell._missilesDictionary);
                Assert.Empty(peer.Network.NetworkSentMessages.GetMessages<NetworkMissileOutcome>());
            });
        });
    }

    [Fact]
    public void QueuedOutcome_ResolvesTheVictimOnTheGameThread()
    {
        RunClients((shooter, peer, native) =>
        {
            peer.Receive(shooter.Shoot());
            NetworkMissileOutcome? outcome = null;
            shooter.Instance.Call(() =>
            {
                shooter.Contact(Assert.Single(shooter.Mission.Shell._missilesDictionary).Value,
                    Mission.MissileCollisionReaction.Stick, Frame(true), shooter.Victim);
                outcome = Assert.Single(shooter.Network.NetworkSentMessages.GetMessages<NetworkMissileOutcome>());
            });
            peer.Instance.Call(() =>
            {
                peer.Handler.DrainPendingShots();
                var registry = peer.Instance.Resolve<INetworkAgentRegistry>();
                Assert.True(registry.RemoveAgent(peer.VictimId));
                // The cosmetic id would still resolve after RemoveAgent, so a poll-thread lookup would pass unnoticed.
                registry.ForgetCosmeticAgent(peer.Victim);
                Assert.False(registry.TryGetCosmeticAgent(peer.VictimId, out _));
            });
            var serializer = new ProtoBufSerializer(new SerializableTypeMapper());
            byte[] wire = MessagePacket.Create(outcome!, serializer).Data;
            peer.Instance.SimulateMessage(this, wire, markGameThread: false);
            Assert.DoesNotContain(native.Attachments, value => ReferenceEquals(value.Victim, peer.Victim));
            peer.Instance.Call(() => Assert.True(peer.Instance.Resolve<INetworkAgentRegistry>()
                .TryRegisterAgent("peer", peer.VictimId, peer.Victim)));
            peer.Instance.PumpGameThread();
            NativeFixture.Attachment attached = Assert.Single(native.Attachments,
                value => ReferenceEquals(value.Victim, peer.Victim));
            AssertFrame(Frame(true), attached.Frame, exact: true);
        });
    }

    [Fact]
    public void ShieldOutcome_WaitsWhileTheVictimHasNoShieldOut()
    {
        RunClients((shooter, peer, native) =>
        {
            peer.Receive(shooter.Shoot());
            peer.Instance.Call(() =>
            {
                peer.Handler.DrainPendingShots();
                native.OffhandSlots[peer.Victim] = EquipmentIndex.None;
            });
            NetworkMissileOutcome? sent = null;
            shooter.Instance.Call(() =>
            {
                shooter.Contact(Assert.Single(shooter.Mission.Shell._missilesDictionary).Value,
                    Mission.MissileCollisionReaction.Stick, Frame(true), shooter.Victim, true);
                sent = Assert.Single(shooter.Network.NetworkSentMessages.GetMessages<NetworkMissileOutcome>());
            });
            peer.Receive(sent!);
            peer.Instance.Call(() =>
            {
                Assert.DoesNotContain(native.Attachments, value => ReferenceEquals(value.Victim, peer.Victim));
                native.OffhandSlots[peer.Victim] = EquipmentIndex.Weapon2;
                peer.Handler.DrainPendingShots();
            });
            peer.Instance.PumpGameThread();
            NativeFixture.Attachment attached = Assert.Single(native.Attachments,
                value => ReferenceEquals(value.Victim, peer.Victim));
            Assert.Equal((int)EquipmentIndex.Weapon2, attached.Index);
        });
    }

    [Fact]
    public void SettledMissilePickup_RetiresTheOtherClientCopy()
    {
        RunClients((shooter, peer, native) =>
        {
            peer.Receive(shooter.Shoot());
            NetworkMissileOutcome? outcome = null;
            shooter.Instance.Call(() =>
            {
                shooter.Contact(Assert.Single(shooter.Mission.Shell._missilesDictionary).Value,
                    Mission.MissileCollisionReaction.BounceBack, Frame(false));
                Assert.Single(shooter.Items.GetAll()).Value.DisableDynamicBody();
                outcome = Assert.Single(shooter.Network.NetworkSentMessages.GetMessages<NetworkMissileOutcome>());
            });
            peer.Receive(outcome!);
            NetworkWeaponPickedup? pickedUp = null;
            peer.Instance.Call(() =>
            {
                peer.Handler.DrainPendingShots();
                SpawnedItemEntity item = Assert.Single(peer.Items.GetAll()).Value;
                Assert.True(item.PhysicsStopped);
                peer.Spawner.TryRemove(item);
                peer.Broker.Publish(this, new WeaponPickedup(peer.Victim, item, EquipmentIndex.Weapon0,
                    peer.Weapon.Item, null, null,
                    new AgentEquipmentData(EquipmentIndex.Weapon0, EquipmentIndex.None, 0),
                    0, 1, 1, 0, true, peer.Weapon));
                pickedUp = Assert.Single(peer.Network.NetworkSentMessages.GetMessages<NetworkWeaponPickedup>());
                Assert.True(pickedUp.IsStuckMissile);
                Assert.Equal(outcome!.WorldItemId, pickedUp.WorldItemId);
            });
            peer.Instance.PumpGameThread();
            shooter.Receive(pickedUp!);
            shooter.Instance.Call(() =>
            {
                Assert.Empty(shooter.Items.GetAll());
                Assert.Equal(1, native.PickupCalls);
            });
        });
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void AttachmentBeforeVictimRegistration_WaitsWithoutRemovingTheFlyingReplay(bool shield)
    {
        RunClients((shooter, peer, native) =>
        {
            peer.Receive(shooter.Shoot());
            Mission.Missile? flying = null;
            peer.Instance.Call(() =>
            {
                peer.Handler.DrainPendingShots();
                flying = Assert.Single(peer.Mission.Shell._missilesDictionary).Value;
                var agents = peer.Instance.Resolve<INetworkAgentRegistry>();
                Assert.True(agents.RemoveAgent(peer.VictimId));
                agents.ForgetCosmeticAgent(peer.Victim);
            });
            NetworkMissileOutcome? outcome = null;
            shooter.Instance.Call(() =>
            {
                shooter.Contact(Assert.Single(shooter.Mission.Shell._missilesDictionary).Value,
                    Mission.MissileCollisionReaction.Stick, Frame(true), shooter.Victim, shield);
                outcome = Assert.Single(shooter.Network.NetworkSentMessages.GetMessages<NetworkMissileOutcome>());
            });
            peer.Receive(outcome!);
            peer.Instance.Call(() =>
            {
                peer.Handler.DrainPendingShots();
                Assert.Same(flying, Assert.Single(peer.Mission.Shell._missilesDictionary).Value);
                Assert.DoesNotContain(flying!.Entity, native.RemovedEntities);
                Assert.DoesNotContain(native.Attachments, value => ReferenceEquals(value.Victim, peer.Victim));
                Assert.True(peer.Instance.Resolve<INetworkAgentRegistry>().TryRegisterAgent("peer", peer.VictimId, peer.Victim));
                peer.Handler.DrainPendingShots();
                var attachment = Assert.Single(native.Attachments, value => ReferenceEquals(value.Victim, peer.Victim));
                Assert.Equal(shield, attachment.Shield);
                AssertFrame(Frame(true), attachment.Frame, exact: true);
                Assert.Empty(peer.Mission.Shell._missilesDictionary);
            });
        });
    }

    [Theory]
    [InlineData("scene")]
    [InlineData("item")]
    [InlineData("ship")]
    public void UnavailableParentOutcome_CachesSceneMissesButRecoversLateRuntimeParents(string identity)
    {
        RunClients((shooter, peer, native) =>
        {
            peer.Receive(shooter.Shoot());
            peer.Instance.Call(() => peer.Handler.DrainPendingShots());
            Guid canonicalId = Guid.NewGuid();
            NetworkMissileOutcome outcome = null!;
            shooter.Instance.Call(() =>
            {
                var parent = native.AddParent(shooter, 37, MatrixFrame.Identity,
                    identity != "scene", identity == "item");
                if (identity == "item") shooter.Items.Register(canonicalId, (SpawnedItemEntity)parent);
                if (identity == "ship")
                    Assert.True(shooter.Ships.TryRegister(new NetworkShipInfo(canonicalId, "shooter", null, false, parent, null)));
                shooter.Contact(Assert.Single(shooter.Mission.Shell._missilesDictionary).Value,
                    Mission.MissileCollisionReaction.Stick, Frame(false), parent: parent);
                outcome = Assert.Single(shooter.Network.NetworkSentMessages.GetMessages<NetworkMissileOutcome>());
            });
            peer.Receive(outcome);
            peer.Instance.Call(() =>
            {
                native.MissionObjectReads.TryGetValue(peer.Mission.Shell, out int reads);
                for (int i = 0; i < 3; i++) peer.Handler.DrainPendingShots();
                native.MissionObjectReads.TryGetValue(peer.Mission.Shell, out int afterRetries);
                Assert.Equal(reads, afterRetries);
                Assert.Empty(peer.Items.GetAll());
                Assert.Single(peer.Mission.Shell._missilesDictionary);
                if (identity == "scene") return;

                var parent = native.AddParent(peer, 75, MatrixFrame.Identity, runtime: true, worldItem: identity == "item");
                if (identity == "item") peer.Items.Register(canonicalId, (SpawnedItemEntity)parent);
                else Assert.True(peer.Ships.TryRegister(new NetworkShipInfo(canonicalId, "shooter", null, false, parent, null)));
                peer.Handler.DrainPendingShots();
                var item = Assert.Single(peer.Items.GetAll(), value => value.Key != canonicalId).Value;
                Assert.Same(parent, peer.Spawner.Parents[item]);
                AssertFrame(Frame(false), peer.Spawner.Frames[item], exact: false);
                Assert.Empty(peer.Mission.Shell._missilesDictionary);
            });
        });
    }

    [Theory]
    [InlineData(false, "scene", false)]
    [InlineData(true, "scene", false)]
    [InlineData(false, "item", false)]
    [InlineData(true, "item", false)]
    [InlineData(false, "ship", false)]
    [InlineData(true, "ship", false)]
    [InlineData(false, "scene", true)]
    [InlineData(true, "scene", true)]
    public void ParentedOutcome_FollowsThePeersMovingParent(bool bounce, string identity, bool scaled)
    {
        RunClients((shooter, peer, native) =>
        {
            peer.Receive(shooter.Shoot());
            MissionObject? sourceParent = null;
            MissionObject? targetParent = null;
            Guid canonicalId = Guid.NewGuid();
            var sourceParentFrame = new MatrixFrame(Mat3.Identity, new Vec3(10f, 20f, 30f));
            var targetParentFrame = new MatrixFrame(Mat3.Identity, new Vec3(40f, 50f, 60f));
            if (scaled)
            {
                sourceParentFrame.rotation.s *= 1.5f;
                sourceParentFrame.rotation.f *= 2f;
                sourceParentFrame.rotation.u *= 0.8f;
                targetParentFrame.rotation = sourceParentFrame.rotation;
            }
            shooter.Instance.Call(() => sourceParent = native.AddParent(shooter, 37, sourceParentFrame,
                identity != "scene", identity == "item"));
            peer.Instance.Call(() => targetParent = native.AddParent(peer, identity == "scene" ? 37 : 75,
                targetParentFrame, identity != "scene", identity == "item"));
            if (identity == "item")
            {
                shooter.Items.Register(canonicalId, (SpawnedItemEntity)sourceParent!);
                peer.Items.Register(canonicalId, (SpawnedItemEntity)targetParent!);
            }
            if (identity == "ship")
            {
                Assert.True(shooter.Ships.TryRegister(new NetworkShipInfo(canonicalId, "shooter", null, false, sourceParent!, null)));
                Assert.True(peer.Ships.TryRegister(new NetworkShipInfo(canonicalId, "shooter", null, false, targetParent!, null)));
            }
            NetworkMissileOutcome? outcome = null;
            MatrixFrame localFrame = Frame(false);
            shooter.Instance.Call(() =>
            {
                MatrixFrame contactFrame = bounce ? sourceParentFrame.TransformToParent(in localFrame) : localFrame;
                shooter.Contact(Assert.Single(shooter.Mission.Shell._missilesDictionary).Value,
                    bounce ? Mission.MissileCollisionReaction.BounceBack : Mission.MissileCollisionReaction.Stick,
                    contactFrame, parent: bounce ? null : sourceParent);
                if (bounce)
                {
                    native.SettleParent = sourceParent;
                    Assert.True(shooter.Items.TryGet(MissileHandler.CreateMissileItemId(
                        Assert.Single(shooter.Network.NetworkSentMessages.GetMessages<NetworkAgentShoot>()).AgentId,
                        Assert.Single(shooter.Network.NetworkSentMessages.GetMessages<NetworkAgentShoot>()).ShotSequence), out var item));
                    item.DisableDynamicBody();
                }
                outcome = Assert.Single(shooter.Network.NetworkSentMessages.GetMessages<NetworkMissileOutcome>());
                Assert.Equal(identity == "scene" ? 37 : (int?)null, outcome.ParentMissionObjectId);
                Assert.Equal(identity == "item" ? canonicalId : (Guid?)null, outcome.ParentWorldItemId);
                Assert.Equal(identity == "ship" ? canonicalId : (Guid?)null, outcome.ParentShipId);
            });
            peer.Receive(outcome!);
            peer.Instance.Call(() =>
            {
                peer.Handler.DrainPendingShots();
                Assert.True(peer.Items.TryGet(outcome!.WorldItemId, out var item));
                Assert.Same(targetParent, peer.Spawner.Parents[item]);
                Assert.True((localFrame.origin - peer.Spawner.LocalFrames[item].origin).Length < 0.0001f);
                MatrixFrame moved = targetParentFrame;
                moved.origin += new Vec3(100f, -20f, 5f);
                moved.rotation.RotateAboutUp(0.7f);
                native.EntityFrames[targetParent!.GameEntity.Pointer] = moved;
                Assert.True(peer.Spawner.TryGetState(item, out var frame, out _));
                Assert.True((frame.origin - moved.TransformToParent(in localFrame).origin).Length < 0.001f);
                if (bounce)
                    Assert.Equal(targetParent.GameEntity.Pointer, native.SnapParents[item]!.Pointer);
                var serializer = new ProtoBufSerializer(new SerializableTypeMapper());
                output.WriteLine($"{outcome.Kind} parent={identity} scaled={scaled}: MessagePacket.Data={MessagePacket.Create(outcome, serializer).Data.Length} bytes");
            });
        });
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void DestructibleOutcome_KeepsTheShootersStateWhenThePeerChangesState(bool destructionBeforeReceipt)
    {
        RunClients((shooter, peer, native) =>
        {
            DestructableComponent source = null!, target = null!;
            GameEntity sourceState = null!, targetState = null!, sourceBroken = null!, targetBroken = null!;
            var parentFrame = new MatrixFrame(Mat3.Identity, new Vec3(10f, 20f, 30f));
            shooter.Instance.Call(() => source = native.AddDestructible(shooter, 37, parentFrame, out sourceState, out sourceBroken));
            peer.Instance.Call(() => target = native.AddDestructible(peer, 37, parentFrame, out targetState, out targetBroken));
            var stateOffset = new MatrixFrame(Mat3.Identity, new Vec3(3f, 4f, 5f));
            native.EntityFrames[sourceState.Pointer] = stateOffset;
            native.EntityFrames[targetState.Pointer] = stateOffset;
            peer.Receive(shooter.Shoot());
            NetworkMissileOutcome outcome = null!;
            SpawnedItemEntity sourceItem = null!, targetItem = null!;
            MatrixFrame attachLocal = default;
            shooter.Instance.Call(() =>
            {
                shooter.Contact(Assert.Single(shooter.Mission.Shell._missilesDictionary).Value,
                    Mission.MissileCollisionReaction.Stick, Frame(false), parent: source);
                sourceItem = Assert.Single(shooter.Items.GetAll()).Value;
                Assert.Same(sourceState, native.ParentOf(sourceItem.GameEntity));
                MatrixFrame sourceFrame = sourceItem.GameEntity.GetGlobalFrame();
                attachLocal = sourceState.GetGlobalFrame().TransformToLocalNonOrthogonal(in sourceFrame);
                outcome = Assert.Single(shooter.Network.NetworkSentMessages.GetMessages<NetworkMissileOutcome>());
            });
            if (destructionBeforeReceipt)
                peer.Instance.Call(() => target.SetDestructionLevel(1, -1, 5f, Vec3.Zero, Vec3.Up, noEffects: true));
            peer.Receive(outcome);
            peer.Instance.Call(() =>
            {
                peer.Handler.DrainPendingShots();
                targetItem = Assert.Single(peer.Items.GetAll()).Value;
                Assert.Same(targetState, native.ParentOf(targetItem.GameEntity));
                Assert.True((sourceItem.GameEntity.GetGlobalFrame().origin - targetItem.GameEntity.GetGlobalFrame().origin).Length < 0.001f);
                target.SetDestructionLevel(1, -1, 5f, Vec3.Zero, Vec3.Up, noEffects: true);
                Assert.Same(targetBroken, target.CurrentState);
            });
            shooter.Instance.Call(() =>
            {
                source.SetDestructionLevel(1, -1, 5f, Vec3.Zero, Vec3.Up, noEffects: true);
                Assert.Same(sourceBroken, source.CurrentState);
            });
            var movingState = new MatrixFrame(Mat3.Identity, new Vec3(50f, 0f, 5f));
            native.EntityFrames[sourceState.Pointer] = movingState;
            native.EntityFrames[targetState.Pointer] = movingState;
            native.EntityFrames[targetBroken.Pointer] = new MatrixFrame(Mat3.Identity, new Vec3(-100f, 0f, 0f));
            Assert.Same(targetState, native.ParentOf(targetItem.GameEntity));
            MatrixFrame expected = parentFrame.TransformToParent(movingState).TransformToParent(attachLocal);
            Assert.True((expected.origin - targetItem.GameEntity.GetGlobalFrame().origin).Length < 0.001f);
            Assert.True((sourceItem.GameEntity.GetGlobalFrame().origin - targetItem.GameEntity.GetGlobalFrame().origin).Length < 0.001f);
            var serializer = new ProtoBufSerializer(new SerializableTypeMapper());
            output.WriteLine($"Ground parent=destructible: MessagePacket.Data={MessagePacket.Create(outcome, serializer).Data.Length} bytes");
        }, realParenting: true);
    }

#if COOP_NAVAL
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void RuntimeShipChildOutcome_ResolvesTheNavalPathAndFollowsTheChild(bool bounce)
    {
        var shipEngine = new global::Missions.Naval.NavalShipEngine(null!, null!, null!, null!, null!, null!, null!, null!, null!);
        RunClients((shooter, peer, native) =>
        {
            MissionObject sourceHull = null!, targetHull = null!, sourceChild = null!, targetChild = null!;
            Guid shipId = Guid.NewGuid();
            var hullFrame = new MatrixFrame(Mat3.Identity, new Vec3(10f, 20f, 30f));
            var childFrame = new MatrixFrame(Mat3.Identity, new Vec3(3f, 4f, 5f));
            shooter.Instance.Call(() =>
            {
                sourceHull = native.AddParent(shooter, 37, hullFrame, runtime: true);
                sourceChild = native.AddShipChild(shooter, sourceHull, 38, childFrame);
                Assert.True(shooter.Ships.TryRegister(new NetworkShipInfo(shipId, "shooter", null, false, sourceHull, null)));
            });
            peer.Instance.Call(() =>
            {
                targetHull = native.AddParent(peer, 70, hullFrame, runtime: true);
                targetChild = native.AddShipChild(peer, targetHull, 71, childFrame);
                Assert.True(peer.Ships.TryRegister(new NetworkShipInfo(shipId, "shooter", null, false, targetHull, null)));
            });
            peer.Receive(shooter.Shoot());
            NetworkMissileOutcome outcome = null!;
            shooter.Instance.Call(() =>
            {
                MatrixFrame local = Frame(false);
                shooter.Contact(Assert.Single(shooter.Mission.Shell._missilesDictionary).Value,
                    bounce ? Mission.MissileCollisionReaction.BounceBack : Mission.MissileCollisionReaction.Stick,
                    bounce ? sourceChild.GameEntity.GetGlobalFrame().TransformToParent(in local) : local,
                    parent: bounce ? null : sourceChild);
                if (bounce)
                {
                    native.SettleParent = sourceChild;
                    Assert.Single(shooter.Items.GetAll()).Value.DisableDynamicBody();
                }
                outcome = Assert.Single(shooter.Network.NetworkSentMessages.GetMessages<NetworkMissileOutcome>());
                Assert.Equal(shipId, outcome.ParentShipId);
                Assert.Equal("0:platform/0:ballista", outcome.ParentShipObjectKey);
                Assert.Equal(1, outcome.ParentShipObjectIndex);
                Assert.Null(outcome.ParentMissionObjectId);
            });
            peer.Receive(outcome);
            peer.Instance.Call(() =>
            {
                peer.Handler.DrainPendingShots();
                var item = Assert.Single(peer.Items.GetAll()).Value;
                Assert.Same(native.EntityOf(targetChild.GameEntity), native.ParentOf(item.GameEntity));
                MatrixFrame movingChild = childFrame;
                movingChild.origin += new Vec3(5f, 0f, 2f);
                movingChild.rotation.RotateAboutUp(0.7f);
                native.EntityFrames[targetChild.GameEntity.Pointer] = movingChild;
                MatrixFrame expected = targetChild.GameEntity.GetGlobalFrame().TransformToParent(Frame(false));
                Assert.True((expected.origin - item.GameEntity.GetGlobalFrame().origin).Length < 0.001f);
                var serializer = new ProtoBufSerializer(new SerializableTypeMapper());
                output.WriteLine($"{outcome.Kind} parent=ship-child key={outcome.ParentShipObjectKey} index={outcome.ParentShipObjectIndex}: MessagePacket.Data={MessagePacket.Create(outcome, serializer).Data.Length} bytes");
            });
        }, realParenting: true, shipEngine);
    }
#endif

    [Fact]
    public void AllUnresolvedCapacity_RejectsNewWorkWithoutEvictingAdmittedShots()
    {
        RunClients((shooter, peer, native) =>
        {
            NetworkAgentShoot shot = shooter.Shoot();
            peer.Receive(shot);
            peer.Instance.Call(() =>
            {
                peer.Handler.DrainPendingShots();
                var agents = peer.Instance.Resolve<INetworkAgentRegistry>();
                Assert.True(agents.RemoveAgent(peer.VictimId));
                agents.ForgetCosmeticAgent(peer.Victim);
            });
            NetworkMissileOutcome outcome = null!;
            shooter.Instance.Call(() =>
            {
                shooter.Contact(Assert.Single(shooter.Mission.Shell._missilesDictionary).Value,
                    Mission.MissileCollisionReaction.Stick, Frame(true), shooter.Victim);
                outcome = Assert.Single(shooter.Network.NetworkSentMessages.GetMessages<NetworkMissileOutcome>());
            });
            peer.Instance.Call(() =>
            {
                for (int i = 1; i <= 8192; i++)
                {
                    long sequence = shot.ShotSequence + i;
                    if (i < 8192)
                        peer.Broker.Publish(this, new NetworkAgentShoot(shot.AgentId, shot.Position, shot.Velocity,
                            shot.Orientation, shot.HasRigidBody, shot.MissileItemId, shot.ItemModifierId, shot.Banner,
                            shot.MissileIndex, shot.BaseSpeed, shot.Speed, shot.CurrentUsageIndex, sequence));
                    peer.Broker.Publish(this, new NetworkMissileOutcome(MissileHandler.CreateMissileItemId(shot.AgentId, sequence),
                        outcome.Kind, outcome.Origin, outcome.Rotation, outcome.VictimId, outcome.BoneIndex,
                        outcome.RemainingLifeTime));
                }
                peer.Handler.DrainPendingShots();
                var unresolved = (System.Collections.IDictionary)AccessTools.Field(typeof(MissileHandler), "replayOutcomes").GetValue(peer.Handler)!;
                var pending = (System.Collections.IDictionary)AccessTools.Field(typeof(MissileHandler), "pendingOutcomes").GetValue(peer.Handler)!;
                Assert.Equal(8192, unresolved.Count);
                int remainingSlots = 8192 - pending.Count;
                for (int i = 0; i < remainingSlots; i++)
                {
                    long sequence = shot.ShotSequence + 8194 + i;
                    peer.Broker.Publish(this, new NetworkMissileOutcome(MissileHandler.CreateMissileItemId(shot.AgentId, sequence),
                        outcome.Kind, outcome.Origin, outcome.Rotation, outcome.VictimId, outcome.BoneIndex,
                        outcome.RemainingLifeTime));
                }
                Assert.Equal(8192, pending.Count);
                peer.Broker.Publish(this, outcome);
                Assert.Equal(8192, pending.Count);
                Assert.False(pending.Contains(outcome.WorldItemId));
                long rejectedSequence = shot.ShotSequence + 8193;
                peer.Broker.Publish(this, new NetworkAgentShoot(shot.AgentId, shot.Position, shot.Velocity,
                    shot.Orientation, shot.HasRigidBody, shot.MissileItemId, shot.ItemModifierId, shot.Banner,
                    shot.MissileIndex, shot.BaseSpeed, shot.Speed, shot.CurrentUsageIndex, rejectedSequence));
                peer.Handler.DrainPendingShots();
                Assert.False(peer.Handler.IsReconstructionPending(shot.AgentId, rejectedSequence));
                Assert.Equal(8192, unresolved.Count);
                Assert.Single(peer.Mission.Shell._missilesDictionary);
                Assert.True(peer.Instance.Resolve<INetworkAgentRegistry>().TryRegisterAgent("peer", peer.VictimId, peer.Victim));
                peer.Handler.DrainPendingShots();
                Assert.Equal(8192, native.Attachments.Count(value => ReferenceEquals(value.Victim, peer.Victim)));
                Assert.Empty(unresolved);
                Assert.Empty(peer.Mission.Shell._missilesDictionary);
            });
        });
    }

    [Fact]
    public void CapacityPressure_EvictsCompletedHistoryBeforeAnUnresolvedShot()
    {
        RunClients((shooter, peer, native) =>
        {
            NetworkAgentShoot shot = shooter.Shoot();
            peer.Receive(shot);
            long secondSequence = shot.ShotSequence + 8193;
            peer.Receive(new NetworkAgentShoot(shot.AgentId, shot.Position, shot.Velocity, shot.Orientation,
                shot.HasRigidBody, shot.MissileItemId, shot.ItemModifierId, shot.Banner, shot.MissileIndex,
                shot.BaseSpeed, shot.Speed, shot.CurrentUsageIndex, secondSequence));
            peer.Instance.Call(() => peer.Handler.DrainPendingShots());
            NetworkMissileOutcome? outcome = null;
            shooter.Instance.Call(() =>
            {
                shooter.Contact(Assert.Single(shooter.Mission.Shell._missilesDictionary).Value,
                    Mission.MissileCollisionReaction.Stick, Frame(true), shooter.Victim);
                outcome = Assert.Single(shooter.Network.NetworkSentMessages.GetMessages<NetworkMissileOutcome>());
            });
            peer.Instance.Call(() =>
            {
                for (int i = 1; i <= 8192; i++)
                {
                    long sequence = shot.ShotSequence + i;
                    peer.Broker.Publish(this, new NetworkAgentShoot(shot.AgentId, shot.Position, shot.Velocity,
                        shot.Orientation, shot.HasRigidBody, shot.MissileItemId, shot.ItemModifierId, shot.Banner,
                        shot.MissileIndex, shot.BaseSpeed, shot.Speed, shot.CurrentUsageIndex, sequence));
                    peer.Broker.Publish(this, new NetworkMissileOutcome(MissileHandler.CreateMissileItemId(shot.AgentId, sequence),
                        outcome!.Kind, outcome.Origin, outcome.Rotation, outcome.VictimId, outcome.BoneIndex,
                        outcome.RemainingLifeTime));
                }
                peer.Broker.Publish(this, new NetworkMissileOutcome(MissileHandler.CreateMissileItemId(shot.AgentId, secondSequence),
                    outcome!.Kind, outcome.Origin, outcome.Rotation, outcome.VictimId, outcome.BoneIndex,
                    outcome.RemainingLifeTime));
                Assert.Single(native.Attachments, value => ReferenceEquals(value.Victim, peer.Victim));
                peer.Handler.DrainPendingShots();
                Assert.Equal(8193, native.Attachments.Count(value => ReferenceEquals(value.Victim, peer.Victim)));
                Assert.Single(peer.Mission.Shell._missilesDictionary);
            });
            peer.Receive(outcome!);
            peer.Instance.Call(() =>
            {
                Assert.Empty(peer.Mission.Shell._missilesDictionary);
                Assert.Equal(8194, native.Attachments.Count(value => ReferenceEquals(value.Victim, peer.Victim)));
            });
        });
    }

    [Fact]
    public void MissingVictim_ExpiresAtTheOriginalDeadlineWithoutRemovingTheReplay()
    {
        RunClients((shooter, peer, native) =>
        {
            peer.Receive(shooter.Shoot());
            peer.Instance.Call(() =>
            {
                peer.Handler.DrainPendingShots();
                var agents = peer.Instance.Resolve<INetworkAgentRegistry>();
                Assert.True(agents.RemoveAgent(peer.VictimId));
                agents.ForgetCosmeticAgent(peer.Victim);
            });
            NetworkMissileOutcome? outcome = null;
            shooter.Instance.Call(() =>
            {
                shooter.Contact(Assert.Single(shooter.Mission.Shell._missilesDictionary).Value,
                    Mission.MissileCollisionReaction.Stick, Frame(true), shooter.Victim);
                outcome = Assert.Single(shooter.Network.NetworkSentMessages.GetMessages<NetworkMissileOutcome>());
            });
            peer.Receive(outcome!);
            peer.Instance.Call(() =>
            {
                var unresolved = (System.Collections.IDictionary)AccessTools.Field(typeof(MissileHandler), "replayOutcomes").GetValue(peer.Handler)!;
                object replay = Assert.Single(unresolved.Values.Cast<object>());
                PropertyInfo expires = AccessTools.Property(replay.GetType(), "Expires");
                DateTime deadline = (DateTime)expires.GetValue(replay)!;
                peer.Handler.DrainPendingShots();
                Assert.Equal(deadline, (DateTime)expires.GetValue(replay)!);
                MethodInfo prune = AccessTools.Method(typeof(MissileHandler), "PruneOutcomes");
                prune.Invoke(peer.Handler, new object[] { deadline.AddTicks(-1) });
                Assert.Single(unresolved);
                Assert.Single(peer.Mission.Shell._missilesDictionary);
                prune.Invoke(peer.Handler, new object[] { deadline });
                Assert.Empty(unresolved);
                Assert.True(peer.Instance.Resolve<INetworkAgentRegistry>().TryRegisterAgent("peer", peer.VictimId, peer.Victim));
                peer.Handler.DrainPendingShots();
                Assert.Single(peer.Mission.Shell._missilesDictionary);
                Assert.DoesNotContain(native.Attachments, value => ReferenceEquals(value.Victim, peer.Victim));
            });
        });
    }

    [Theory]
    [InlineData(MissileOutcomeKind.Ground, false)]
    [InlineData(MissileOutcomeKind.Ground, true)]
    [InlineData(MissileOutcomeKind.BounceSettled, false)]
    [InlineData(MissileOutcomeKind.Body, true)]
    [InlineData(MissileOutcomeKind.Shield, true)]
    public void OutcomeWire_RoundTripsFramesAndMeasuresBytes(MissileOutcomeKind kind, bool exact)
    {
        RunClients((shooter, peer, native) =>
        {
            shooter.Shoot();
            shooter.Instance.Call(() =>
            {
                Mission.Missile missile = Assert.Single(shooter.Mission.Shell._missilesDictionary).Value;
                MatrixFrame frame = Frame(exact);
                bool attach = kind is MissileOutcomeKind.Body or MissileOutcomeKind.Shield;
                shooter.Contact(missile, kind == MissileOutcomeKind.BounceSettled
                    ? Mission.MissileCollisionReaction.BounceBack : Mission.MissileCollisionReaction.Stick,
                    frame, attach ? shooter.Victim : null, kind == MissileOutcomeKind.Shield);
                if (kind == MissileOutcomeKind.BounceSettled)
                    Assert.Single(shooter.Items.GetAll()).Value.DisableDynamicBody();
                var original = Assert.Single(shooter.Network.NetworkSentMessages.GetMessages<NetworkMissileOutcome>());
                var serializer = new ProtoBufSerializer(new SerializableTypeMapper());
                var packet = MessagePacket.Create(original, serializer);
                var copy = Assert.IsType<NetworkMissileOutcome>(serializer.Deserialize<IMessage>(packet.Data));
                Assert.Equal(original.WorldItemId, copy.WorldItemId);
                Assert.Equal(original.Kind, copy.Kind);
                Assert.Equal(frame.origin, copy.Origin);
                Assert.Equal(original.Rotation, copy.Rotation);
                Assert.Equal(exact ? 9 : 4, copy.Rotation.Length);
                Assert.Equal(attach ? shooter.VictimId : (Guid?)null, copy.VictimId);
                Assert.Equal(original.BoneIndex, copy.BoneIndex);
                Assert.Equal(kind == MissileOutcomeKind.Shield ? 0 : original.BoneIndex, copy.BoneIndex);
                Assert.Equal(original.ParentMissionObjectId, copy.ParentMissionObjectId);
                Assert.Equal(original.ParentWorldItemId, copy.ParentWorldItemId);
                Assert.Equal(original.ParentShipId, copy.ParentShipId);
                Assert.Equal(original.RemainingLifeTime, copy.RemainingLifeTime);
                output.WriteLine($"{kind} exact={exact}: MessagePacket.Data={packet.Data.Length} bytes; rotation={copy.Rotation.Length * 4} bytes");
            });
        });
    }

    private void RunClients(Action<Client, Client, NativeFixture> test, bool realParenting = false, INavalShipEngine? shipEngine = null)
    {
        using var engine = new MissionEngineFixture();
        using var native = new NativeFixture();
        if (realParenting) native.EnableRealParenting();
        EnvironmentInstance first = Clients.First();
        EnvironmentInstance second = Clients.Skip(1).First();
        SetControllerId(first, "shooter");
        SetControllerId(second, "peer");
        ItemObject? item = null;
        first.Call(() =>
        {
            using var allowed = new AllowedThread();
            item = new ItemObject { StringId = "outcome_javelin" };
            item.AddWeapon(new WeaponComponentData(item, WeaponClass.Javelin,
                WeaponFlags.RangedWeapon | WeaponFlags.Consumable), null);
#if DEBUG
            item.Weight = 1f;
            item.PrimaryWeapon.WeaponLength = 100;
            item.PrimaryWeapon.WeaponFlags |= WeaponFlags.AmmoSticksWhenShot;
#endif
        });
        Guid shooterId = Guid.NewGuid();
        Guid victimId = Guid.NewGuid();
        Client? shooter = null;
        Client? peer = null;
        first.Call(() => shooter = new Client(first, engine, native, item!, shooterId, victimId, shipEngine));
        second.Call(() => peer = new Client(second, engine, native, item!, shooterId, victimId, shipEngine));
        native.ApplyProductPatches();
        try { test(shooter!, peer!, native); }
        finally
        {
            first.Call(() => shooter!.Dispose());
            second.Call(() => peer!.Dispose());
        }
    }

    private static MatrixFrame Frame(bool skewed)
    {
        Mat3 rotation = Mat3.Identity;
        rotation.RotateAboutUp(0.63f);
        rotation.RotateAboutSide(-0.21f);
        if (skewed) rotation.s *= 1.15f;
        return new MatrixFrame(rotation, new Vec3(123.456f, -78.901f, 2.345f));
    }

    private static void AssertFrame(MatrixFrame expected, MatrixFrame actual, bool exact)
    {
        Assert.Equal(expected.origin, actual.origin);
        if (exact)
        {
            Assert.Equal(expected.rotation.s, actual.rotation.s);
            Assert.Equal(expected.rotation.f, actual.rotation.f);
            Assert.Equal(expected.rotation.u, actual.rotation.u);
        }
        else
        {
            Assert.True((expected.rotation.s - actual.rotation.s).Length < 0.000002f);
            Assert.True((expected.rotation.f - actual.rotation.f).Length < 0.000002f);
            Assert.True((expected.rotation.u - actual.rotation.u).Length < 0.000002f);
        }
    }

    public class EmptyEngineClassTable : DispatchProxy
    {
        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            if (targetMethod!.Name == "GetClassTypeDefinitionCount") return 0;
            throw new InvalidOperationException("Unexpected native call: " + targetMethod.Name);
        }
    }

    public class EntityEngine : DispatchProxy
    {
        public Func<MethodInfo, object?[], object?> Call { get; set; } = null!;
        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args) => Call(targetMethod!, args!);
    }

    private sealed class Client : IDisposable
    {
        private readonly NativeFixture native;
        public EnvironmentInstance Instance { get; }
        public MockMission Mission { get; }
        public Agent Shooter { get; }
        public Agent Victim { get; }
        public Guid VictimId { get; }
        public MissionWeapon Weapon { get; }
        public IMessageBroker Broker { get; }
        public MockBattleNetwork Network { get; }
        public NetworkWorldItemRegistry Items { get; } = new();
        public NetworkShipRegistry Ships { get; } = new();
        public RecordingSpawner Spawner { get; }
        public MissileHandler Handler { get; }
        private readonly WeaponDropHandler drops;
        private readonly WeaponPickupHandler pickups;

        public Client(EnvironmentInstance instance, MissionEngineFixture engine, NativeFixture native,
            ItemObject item, Guid shooterId, Guid victimId, INavalShipEngine? shipEngine)
        {
            this.native = native;
            Instance = instance;
            VictimId = victimId;
            Mission = engine.CreateMission(instance);
            if (native.RealParenting)
            {
                Mission.Shell.Scene = ObjectHelper.SkipConstructor<Scene>();
                GC.SuppressFinalize(Mission.Shell.Scene);
                Mission.Shell.CurrentState = TaleWorlds.MountAndBlade.Mission.State.Continuing;
            }
            Mission.Shell._missilesDictionary = new Dictionary<int, Mission.Missile>();
            Mission.Shell._missilesList = new MBList<Mission.Missile>();
            AccessTools.Field(typeof(Mission), "_missionObjects").SetValue(Mission.Shell, new MBList<MissionObject>());
            Shooter = Mission.SpawnAgent(new AgentBuildData(Game.Current.PlayerTroop).Controller(AgentControllerType.None));
            Victim = Mission.SpawnAgent(new AgentBuildData(Game.Current.PlayerTroop).Controller(AgentControllerType.None));
            var agents = instance.Resolve<INetworkAgentRegistry>();
            Assert.True(agents.TryRegisterAgent("shooter", shooterId, Shooter));
            Assert.True(agents.TryRegisterAgent("peer", victimId, Victim));
            MBObjectManager.Instance.RegisterObject(item);
            var weapon = new MissionWeapon(item, null, null);
            weapon.Amount = 1;
            Weapon = weapon;
            Assert.True(instance.Resolve<IObjectManager>().AddExisting(item.StringId, item));
            Broker = instance.Resolve<IMessageBroker>();
            Network = instance.Resolve<MockBattleNetwork>();
            Network.RouteMessages = false;
            Spawner = new RecordingSpawner(native);
            native.Spawners[Mission.Shell] = Spawner;
            IWeaponDropWorldItemSpawner spawner = native.RealParenting ? new WeaponDropWorldItemSpawner() : Spawner;
            Handler = new MissileHandler(Network, Broker, agents, spawner, Items, new MissionObjectPath(), shipEngine);
            drops = new WeaponDropHandler(agents, Items, Broker, Network, instance.Resolve<IObjectManager>(), spawner);
            pickups = new WeaponPickupHandler(agents, Items, Network, Broker, instance.Resolve<IObjectManager>());
            var controller = ObjectHelper.SkipConstructor<CoopBattleController>();
            var component = new Mock<ICoopMissionComponent>();
            component.SetupGet(value => value.MissileHandler).Returns(Handler);
            component.SetupGet(value => value.AgentRegistry).Returns(agents);
            component.SetupGet(value => value.ShipRegistry).Returns(Ships);
            typeof(CoopMissionController).GetField("coopMissionComponent", BindingFlags.Instance | BindingFlags.NonPublic)!
                .SetValue(controller, component.Object);
            Mission.Shell.MissionBehaviors.Add(controller);
        }

        public NetworkAgentShoot Shoot()
        {
            NetworkAgentShoot? shot = null;
            Instance.Call(() =>
            {
                var missile = new Mission.Missile(Mission.Shell, 7, native.NewEntity(), Shooter, Weapon, null);
                Mission.Shell._missilesDictionary[7] = missile;
                Mission.Shell._missilesList.Add(missile);
                Broker.Publish(this, new AgentShoot(Shooter, Weapon, Vec3.Zero, Vec3.Forward, Mat3.Identity,
                    50f, 50f, false, 7));
                shot = Assert.Single(Network.NetworkSentMessages.GetMessages<NetworkAgentShoot>());
            });
            return shot!;
        }

        public void Receive(IMessage message)
        {
            var serializer = new ProtoBufSerializer(new SerializableTypeMapper());
            byte[] wire = MessagePacket.Create(message, serializer).Data;
            Instance.Call(() => Broker.Publish(this, serializer.Deserialize<IMessage>(wire)));
        }

        public void Contact(Mission.Missile missile, Mission.MissileCollisionReaction reaction,
            MatrixFrame? frame = null, Agent? victim = null, bool shield = false, MissionObject? parent = null)
        {
            Mission.Shell.HandleMissileCollisionReaction(missile.Index, reaction, frame ?? MatrixFrame.Identity,
                true, missile.ShooterAgent, victim, shield, 7, parent, Vec3.Zero, Vec3.Zero, -1);
            Mission.Shell.OnMissileRemoved(missile.Index);
        }

        public void Dispose()
        {
            Handler.Dispose();
            drops.Dispose();
            pickups.Dispose();
        }
    }

    private sealed class RecordingSpawner : IWeaponDropWorldItemSpawner
    {
        private readonly NativeFixture native;
        public Dictionary<SpawnedItemEntity, MatrixFrame> Frames { get; } = new();
        public Dictionary<SpawnedItemEntity, MissionObject> Parents { get; } = new();
        public Dictionary<SpawnedItemEntity, MatrixFrame> LocalFrames { get; } = new();
        public int SpawnCount { get; private set; }
        public bool IsReady { get; set; } = true;
        public RecordingSpawner(NativeFixture native) => this.native = native;
        public bool IsPresent(SpawnedItemEntity item) => Frames.ContainsKey(item);
        public bool IsRemoved(SpawnedItemEntity item) => !Frames.ContainsKey(item);
        public bool TryGetState(SpawnedItemEntity item, out MatrixFrame frame, out float remainingLifeTime)
        {
            remainingLifeTime = 175f;
            if (Parents.TryGetValue(item, out var parent))
                Frames[item] = parent.GameEntity.GetGlobalFrame().TransformToParent(LocalFrames[item]);
            return Frames.TryGetValue(item, out frame);
        }
        public bool TrySpawn(ref MissionWeapon weapon, Mission.WeaponSpawnFlags spawnFlags, bool hasLifeTime,
            float remainingLifeTime, MatrixFrame frame, out SpawnedItemEntity item, MissionObject? parent = null,
            GameEntity? attachmentParent = null)
        {
            SpawnCount++;
            item = ObjectHelper.SkipConstructor<SpawnedItemEntity>();
            item._weapon = weapon;
            item.SpawnFlags = spawnFlags;
            item.Id = new MissionObjectId(100 + SpawnCount, true);
            GameEntity entity = native.NewEntity();
            item._ownerGameEntity = entity;
            item._gameEntity = entity.WeakEntity;
            Frames.Add(item, parent == null ? frame : parent.GameEntity.GetGlobalFrame().TransformToParent(in frame));
            if (parent != null)
            {
                Parents[item] = parent;
                LocalFrames[item] = frame;
                native.SetParent(entity, parent);
            }
            native.ItemSpawners[item] = this;
            return true;
        }
        public bool TryRemove(SpawnedItemEntity item) => Frames.Remove(item);
    }

    private sealed partial class NativeFixture : IDisposable
    {
        private static NativeFixture current = null!;
        private readonly Harmony harmony = new("e2e.missile-outcome." + Guid.NewGuid().ToString("N"));
        private readonly Dictionary<GameEntity, SpawnedItemEntity?> entities = new();
        public Dictionary<UIntPtr, MatrixFrame> EntityFrames { get; } = new();
        public Dictionary<Mission, RecordingSpawner> Spawners { get; } = new();
        public Dictionary<SpawnedItemEntity, RecordingSpawner> ItemSpawners { get; } = new();
        public HashSet<GameEntity> RemovedEntities { get; } = new();
        public List<Attachment> Attachments { get; } = new();
        public Dictionary<Agent, EquipmentIndex> OffhandSlots { get; } = new();
        public Dictionary<Mission, int> MissionObjectReads { get; } = new();
        public int ReconstructedMissiles { get; private set; }
        public int SnapCalls { get; private set; }
        public int PickupCalls { get; private set; }
        public MatrixFrame? DropFrameOverride { get; set; }
        public MissionObject? SettleParent { get; set; }
        public Dictionary<SpawnedItemEntity, GameEntity?> SnapParents { get; } = new();
        private readonly Dictionary<UIntPtr, GameEntity> parents = new();
        private readonly Dictionary<UIntPtr, List<GameEntity>> children = new();
        private readonly Dictionary<UIntPtr, List<ScriptComponentBehavior>> scripts = new();
        private readonly Dictionary<UIntPtr, string> names = new();
        private FieldInfo? entityBridge;
        private object? originalEntityBridge;
        public bool RealParenting { get; private set; }
        public sealed record Attachment(Agent Victim, bool Shield, int Index, MatrixFrame Frame);

        public NativeFixture()
        {
            current = this;
            Patch(typeof(ScriptComponentBehavior), "CacheEditableFieldsForAllScriptComponents", nameof(Skip));
            harmony.Patch(AccessTools.Method(typeof(GameEntity), nameof(GameEntity.GetFirstScriptOfType))
                .MakeGenericMethod(typeof(SpawnedItemEntity)), prefix: Method(nameof(GetItem)));
            Patch(typeof(GameEntity), nameof(GameEntity.Remove), nameof(RemoveEntity));
            Patch(typeof(GameEntity), nameof(GameEntity.SetVisibilityExcludeParents), nameof(Skip));
            Patch(typeof(GameEntity), nameof(GameEntity.CreateFromWeakEntity), nameof(FromWeak));
            Patch(typeof(WeakGameEntity), nameof(WeakGameEntity.GetGlobalFrame), nameof(WeakGlobalFrame));
            Patch(typeof(WeakGameEntity), "get_Parent", nameof(WeakParent));
            Patch(typeof(MissionWeapon), nameof(MissionWeapon.GetWeaponData), nameof(SkipWeaponData));
            Patch(typeof(MissionWeapon), nameof(MissionWeapon.GetWeaponStatsDataForUsage), nameof(SkipStats));
            Patch(typeof(Mission), nameof(Mission.AddMissileSingleUsageAux), nameof(AddMissile));
            Patch(typeof(Mission), nameof(Mission.PrepareMissileWeaponForDrop), nameof(Skip));
            Patch(typeof(Mission), nameof(Mission.RemoveMissileAsClient), nameof(RemoveMissile));
            Patch(typeof(Mission), nameof(Mission.SpawnWeaponAsDropFromMissile), nameof(DropMissile));
            Patch(typeof(Agent), nameof(Agent.AttachWeaponToBone), nameof(AttachBody));
            Patch(typeof(Agent), nameof(Agent.AttachWeaponToWeapon), nameof(AttachShield));
            Patch(typeof(Agent), nameof(Agent.GetOffhandWieldedItemIndex), nameof(Offhand));
            Patch(typeof(Mission), "get_MissionObjects", nameof(ReadMissionObjects));
            Patch(typeof(SpawnedItemEntity), nameof(SpawnedItemEntity.DisableDynamicBody), nameof(DisablePhysics));
            Patch(typeof(SpawnedItemEntity), nameof(SpawnedItemEntity.StopPhysicsAndSetFrameForClient), nameof(Snap));
            Patch(typeof(WeaponPickupHandler), "ApplyWorldItemPickup", nameof(Pickup));
            Patch(typeof(WeaponPickupHandler), nameof(WeaponPickupHandler.IsWorldItemAvailable), nameof(AvailableItem));
            Patch(typeof(Agent), nameof(Agent.EquipWeaponWithNewEntity), nameof(Equip));
            Patch(typeof(AgentEquipmentData), nameof(AgentEquipmentData.Apply), nameof(Skip));
        }

        public void EnableRealParenting()
        {
            // Initialize NativeObject under the empty engine-class table before allocating a Scene shell.
            NewEntity();
            RealParenting = true;
            entityBridge = AccessTools.Field(AccessTools.TypeByName("TaleWorlds.Engine.EngineApplicationInterface"), "IGameEntity");
            originalEntityBridge = entityBridge.GetValue(null);
            object proxy = typeof(DispatchProxy).GetMethod(nameof(DispatchProxy.Create), Type.EmptyTypes)!
                .MakeGenericMethod(entityBridge.FieldType, typeof(EntityEngine)).Invoke(null, null)!;
            ((EntityEngine)proxy).Call = InvokeEntity;
            entityBridge.SetValue(null, proxy);
            harmony.Patch(AccessTools.Method(typeof(GameEntityExtensions), nameof(GameEntityExtensions.Instantiate),
                new[] { typeof(Scene), typeof(MissionWeapon), typeof(bool), typeof(bool) }), prefix: Method(nameof(InstantiateWeapon)));
            Patch(typeof(Mission), "SpawnWeaponAux", nameof(InitializeWeapon));
            // Rebuild callers after the terminal native stubs, keeping vanilla's managed parenting intact.
            harmony.Patch(AccessTools.Method(typeof(Mission), nameof(Mission.SpawnWeaponWithNewEntityAux)),
                postfix: Method(nameof(RebuildCollision)));
        }

        public GameEntity EntityOf(WeakGameEntity entity) => entities.Keys.Single(value => value.Pointer == entity.Pointer);
        public GameEntity? ParentOf(WeakGameEntity entity) => parents.GetValueOrDefault(entity.Pointer);
        public void SetParent(GameEntity entity, MissionObject parent) => parents[entity.Pointer] = EntityOf(parent.GameEntity);

        public MatrixFrame GlobalFrame(UIntPtr pointer) => parents.TryGetValue(pointer, out var parent)
            ? GlobalFrame(parent.Pointer).TransformToParent(EntityFrames[pointer]) : EntityFrames[pointer];

        private object? InvokeEntity(MethodInfo method, object?[] args)
        {
            UIntPtr pointer = args.Length > 0 && args[0] is UIntPtr value ? value : UIntPtr.Zero;
            switch (method.Name)
            {
                case "GetName": return names[pointer];
                case "GetParentPointer": return parents.GetValueOrDefault(pointer)?.Pointer ?? UIntPtr.Zero;
                case "GetParent": return parents.GetValueOrDefault(pointer);
                case "GetChildCount": return children[pointer].Count;
                case "GetChildPointer": return children[pointer][(int)args[1]!].Pointer;
                case "GetChild": return children[pointer][(int)args[1]!];
                case "GetScriptComponentCount": return scripts[pointer].Count;
                case "GetScriptComponentAtIndex": return scripts[pointer][(int)args[1]!];
                case "GetGlobalFrame": args[1] = GlobalFrame(pointer); return null;
                case "SetGlobalFrame":
                    var frame = (MatrixFrame)args[1]!;
                    EntityFrames[pointer] = parents.TryGetValue(pointer, out var frameParent)
                        ? GlobalFrame(frameParent.Pointer).TransformToLocalNonOrthogonal(in frame) : frame;
                    return null;
                case "AddChild":
                    var childPointer = (UIntPtr)args[1]!;
                    MatrixFrame global = GlobalFrame(childPointer);
                    if (parents.TryGetValue(childPointer, out var oldParent))
                        children[oldParent.Pointer].RemoveAll(child => child.Pointer == childPointer);
                    parents[childPointer] = entities.Keys.Single(entity => entity.Pointer == pointer);
                    children[pointer].Add(entities.Keys.Single(entity => entity.Pointer == childPointer));
                    if ((bool)args[2]!)
                        EntityFrames[childPointer] = GlobalFrame(pointer).TransformToLocalNonOrthogonal(in global);
                    return null;
                case "CreateAndAddScriptComponent":
                    var entity = entities.Keys.Single(entity => entity.Pointer == pointer);
                    var item = ObjectHelper.SkipConstructor<SpawnedItemEntity>();
                    item._ownerGameEntity = entity;
                    item._gameEntity = entity.WeakEntity;
                    item.Id = new MissionObjectId(100 + entities.Count, true);
                    entities[entity] = item;
                    scripts[pointer].Add(item);
                    Mission.Current._missionObjects.Add(item);
                    return null;
                case "GetSkeleton": return null;
                case "GetBodyFlags": return (uint)0;
                case "SetVisibilityExcludeParents":
                case "SetPhysicsState":
                case "BurstEntityParticle":
                case "SetFrameChanged": return null;
                default: throw new InvalidOperationException("Unexpected native entity call: " + method.Name);
            }
        }

        private static bool InstantiateWeapon(ref GameEntity __result)
        {
            __result = current.NewEntity();
            return false;
        }

        private static bool InitializeWeapon(WeakGameEntity weaponEntity, MissionWeapon weapon,
            Mission.WeaponSpawnFlags spawnFlags, bool hasLifeTime, ref SpawnedItemEntity __result)
        {
            __result = current.entities[current.EntityOf(weaponEntity)]!;
            __result._weapon = weapon;
            __result.SpawnFlags = spawnFlags;
            __result._hasLifeTime = hasLifeTime;
            __result._deletionTimer = new TaleWorlds.Core.Timer(Mission.Current.CurrentTime, 175f);
            return false;
        }

        public void ApplyProductPatches()
        {
            var targets = new[]
            {
                (typeof(MissileCollisionPatch), typeof(Mission), nameof(Mission.HandleMissileCollisionReaction)),
                (typeof(MissileItemDropPatch), typeof(Mission), nameof(Mission.SpawnWeaponAsDropFromMissile)),
                (typeof(MissileRemovedPatch), typeof(Mission), nameof(Mission.OnMissileRemoved)),
                (typeof(MissilePhysicsSettledPatch), typeof(SpawnedItemEntity), nameof(SpawnedItemEntity.DisableDynamicBody)),
#if DEBUG
                (typeof(MissileForcedBouncePatch), typeof(Mission), nameof(Mission.MissileHitCallback)),
#endif
            };
            foreach (var (patchType, targetType, name) in targets)
            {
                MethodInfo target = AccessTools.Method(targetType, name);
                Patches? patches = Harmony.GetPatchInfo(target);
                if (patches == null || !patches.Prefixes.Concat(patches.Postfixes).Concat(patches.Transpilers)
                    .Any(patch => patch.PatchMethod.DeclaringType == patchType))
                    harmony.CreateClassProcessor(patchType).Patch();
            }
            // Rebuild after native stubs are installed, including when the environment already applied the category.
            harmony.Patch(AccessTools.Method(typeof(Mission), nameof(Mission.HandleMissileCollisionReaction)),
                postfix: Method(nameof(RebuildCollision)));
        }

        public GameEntity NewEntity()
        {
            var managed = AccessTools.Field(AccessTools.TypeByName("TaleWorlds.DotNet.LibraryApplicationInterface"), "IManaged");
            object? original = managed.GetValue(null);
            managed.SetValue(null, typeof(DispatchProxy).GetMethod(nameof(DispatchProxy.Create), Type.EmptyTypes)!
                .MakeGenericMethod(managed.FieldType, typeof(EmptyEngineClassTable)).Invoke(null, null));
            try
            {
                var entity = ObjectHelper.SkipConstructor<GameEntity>();
                GC.SuppressFinalize(entity);
                AccessTools.DeclaredProperty(AccessTools.TypeByName("TaleWorlds.DotNet.NativeObject"), "Pointer")
                    .SetValue(entity, new UIntPtr((uint)entities.Count + 1));
                entities.Add(entity, null);
                EntityFrames[entity.Pointer] = MatrixFrame.Identity;
                children[entity.Pointer] = new List<GameEntity>();
                scripts[entity.Pointer] = new List<ScriptComponentBehavior>();
                names[entity.Pointer] = "entity";
                return entity;
            }
            finally
            {
                managed.SetValue(null, original);
            }
        }

        public MissionObject AddParent(Client client, int id, MatrixFrame frame, bool runtime = false, bool worldItem = false)
        {
            MissionObject parent = worldItem ? ObjectHelper.SkipConstructor<SpawnedItemEntity>()
                : ObjectHelper.SkipConstructor<SynchedMissionObject>();
            parent.Id = new MissionObjectId(id, runtime);
            GameEntity entity = NewEntity();
            parent._gameEntity = entity.WeakEntity;
            EntityFrames[entity.Pointer] = frame;
            client.Mission.Shell._missionObjects.Add(parent);
            scripts[entity.Pointer].Add(parent);
            if (parent is SpawnedItemEntity item)
            {
                item._ownerGameEntity = entity;
                client.Spawner.Frames[item] = frame;
                ItemSpawners[item] = client.Spawner;
            }
            return parent;
        }

        public MissionObject AddShipChild(Client client, MissionObject hull, int id, MatrixFrame localFrame)
        {
            var platform = NewEntity();
            names[platform.Pointer] = "platform";
            hull.GameEntity.AddChild(platform.WeakEntity);
            var child = AddParent(client, id, localFrame, runtime: true);
            names[child.GameEntity.Pointer] = "ballista";
            platform.AddChild(EntityOf(child.GameEntity));
            var other = ObjectHelper.SkipConstructor<SynchedMissionObject>();
            other._gameEntity = child.GameEntity;
            scripts[child.GameEntity.Pointer].Insert(0, other);
            return child;
        }

        public DestructableComponent AddDestructible(Client client, int id, MatrixFrame frame,
            out GameEntity state, out GameEntity broken)
        {
            var root = NewEntity();
            EntityFrames[root.Pointer] = frame;
            var parent = ObjectHelper.SkipConstructor<DestructableComponent>();
            parent._gameEntity = root.WeakEntity;
            parent.Id = new MissionObjectId(id, false);
            scripts[root.Pointer].Add(parent);
            client.Mission.Shell._missionObjects.Add(parent);
            state = NewEntity();
            broken = NewEntity();
            names[broken.Pointer] = "broken";
            root.AddChild(state);
            root.AddChild(broken);
            parent.CurrentState = state;
            parent._originalState = state;
            parent._referenceEntity = root;
            parent._destructionStates = new[] { "broken" };
            return parent;
        }
        private void Patch(Type type, string target, string prefix) =>
            harmony.Patch(AccessTools.Method(type, target), prefix: Method(prefix));
        private static HarmonyMethod Method(string name) => new(typeof(NativeFixture), name) { priority = Priority.First };
        private static bool Skip() => false;
        private static void RebuildCollision() { }
        private static bool SkipWeaponData(ref WeaponData __result) { __result = default; return false; }
        private static bool SkipStats(ref WeaponStatsData __result) { __result = default; return false; }
        private static bool GetItem(GameEntity __instance, ref SpawnedItemEntity? __result)
        {
            if (!current.entities.TryGetValue(__instance, out __result)) return true;
            return false;
        }
        private static bool RemoveEntity(GameEntity __instance)
        {
            current.RemovedEntities.Add(__instance);
            return false;
        }
        private static bool FromWeak(WeakGameEntity weakEntity, ref GameEntity? __result)
        {
            __result = current.entities.Keys.Single(value => value.Pointer == weakEntity.Pointer);
            return false;
        }
        private static bool WeakGlobalFrame(WeakGameEntity __instance, ref MatrixFrame __result)
        {
            if (!current.EntityFrames.ContainsKey(__instance.Pointer)) return true;
            __result = current.GlobalFrame(__instance.Pointer);
            return false;
        }
        private static bool WeakParent(WeakGameEntity __instance, ref WeakGameEntity __result)
        {
            __result = current.ParentOf(__instance)?.WeakEntity ?? WeakGameEntity.Invalid;
            return false;
        }
        private static bool AddMissile(out GameEntity missileEntity, ref int __result)
        {
            current.ReconstructedMissiles++;
            missileEntity = current.NewEntity();
            __result = 50 + current.ReconstructedMissiles;
            return false;
        }
        private static bool RemoveMissile(Mission __instance, int missileIndex)
        {
            __instance.OnMissileRemoved(missileIndex);
            return false;
        }
        private static bool DropMissile(Mission __instance, int missileIndex, MissionObject? attachedMissionObject,
            in MatrixFrame attachLocalFrame,
            Mission.WeaponSpawnFlags spawnFlags, ref MissionObjectId __result)
        {
            if (current.RealParenting) return true;
            Mission.Missile missile = __instance._missilesDictionary[missileIndex];
            MissionWeapon weapon = missile.Weapon;
            current.Spawners[__instance].TrySpawn(ref weapon, spawnFlags, true, 175f,
                current.DropFrameOverride ?? attachLocalFrame, out var item, attachedMissionObject);
            current.entities[missile.Entity] = item;
            __result = item.Id;
            return false;
        }
        private static bool AttachBody(Agent __instance, sbyte boneIndex, ref MatrixFrame attachLocalFrame)
        {
            current.Attachments.Add(new Attachment(__instance, false, boneIndex, attachLocalFrame));
            return false;
        }
        private static bool AttachShield(Agent __instance, EquipmentIndex slotIndex, ref MatrixFrame attachLocalFrame)
        {
            current.Attachments.Add(new Attachment(__instance, true, (int)slotIndex, attachLocalFrame));
            return false;
        }
        private static bool Offhand(Agent __instance, ref EquipmentIndex __result)
        {
            __result = current.OffhandSlots.TryGetValue(__instance, out var slot) ? slot : EquipmentIndex.Weapon1;
            return false;
        }
        private static void ReadMissionObjects(Mission __instance)
        {
            current.MissionObjectReads.TryGetValue(__instance, out int reads);
            current.MissionObjectReads[__instance] = reads + 1;
        }
        private static bool DisablePhysics(SpawnedItemEntity __instance)
        {
            __instance._physicsStopped = true;
            if (current.SettleParent != null)
            {
                if (current.RealParenting)
                {
                    MatrixFrame global = __instance.GameEntity.GetGlobalFrame();
                    __instance._groundEntityWhenDisabled = current.EntityOf(current.SettleParent.GameEntity);
                    current.SettleParent.GameEntity.AddChild(__instance.GameEntity);
                    __instance.GameEntity.SetGlobalFrame(in global);
                    return false;
                }
                var spawner = current.ItemSpawners[__instance];
                MatrixFrame frame = spawner.Frames[__instance];
                var parent = current.SettleParent;
                __instance._groundEntityWhenDisabled = current.entities.Keys.Single(value => value.Pointer == parent.GameEntity.Pointer);
                spawner.Parents[__instance] = parent;
                spawner.LocalFrames[__instance] = parent.GameEntity.GetGlobalFrame().TransformToLocalNonOrthogonal(in frame);
            }
            return false;
        }
        private static bool Snap(SpawnedItemEntity __instance, MatrixFrame frame, GameEntity? parent)
        {
            current.SnapCalls++;
            __instance._physicsStopped = true;
            current.SnapParents[__instance] = parent;
            if (current.RealParenting)
            {
                parent?.AddChild(__instance._ownerGameEntity);
                MatrixFrame global = parent == null ? frame : parent.GetGlobalFrame().TransformToParent(in frame);
                __instance.GameEntity.SetGlobalFrame(in global);
                return false;
            }
            current.ItemSpawners[__instance].Frames[__instance] = parent == null
                ? frame : current.EntityFrames[parent.Pointer].TransformToParent(in frame);
            return false;
        }
        private static bool Pickup() { current.PickupCalls++; return false; }
        private static bool AvailableItem(ref bool __result) { __result = true; return false; }
        private static bool Equip(Agent __instance, EquipmentIndex slotIndex, ref MissionWeapon weapon)
        {
            __instance.Equipment[slotIndex] = weapon;
            return false;
        }
        public void Dispose()
        {
            harmony.UnpatchAll(harmony.Id);
#if DEBUG
            if (bounceModelsInstalled) MissionGameModels.Current = previousBounceModels;
            if (bounceVoiceBridge != null) bounceVoiceBridge.SetValue(null, previousBounceVoices);
#endif
            if (entityBridge != null) entityBridge.SetValue(null, originalEntityBridge);
            current = null!;
        }
    }
}
