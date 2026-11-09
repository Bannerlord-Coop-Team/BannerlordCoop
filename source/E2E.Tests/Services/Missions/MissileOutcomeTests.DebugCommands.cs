#if DEBUG
using Common.Commands;
using Common.Util;
using E2E.Tests.Environment.MockEngine;
using GameInterface.Services.MapEvents;
using HarmonyLib;
using Missions;
using Missions.Battles;
using Missions.Messages;
using Missions.Missiles.Message;
using Missions.Missiles.Patches;
using Moq;
using Newtonsoft.Json.Linq;
using System.Globalization;
using System.Reflection;
using TaleWorlds.Core;
using TaleWorlds.Engine;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
using TaleWorlds.MountAndBlade.ComponentInterfaces;
using TaleWorlds.ModuleManager;
using Commands = global::Missions.Missiles.MissileDebugCommands;

namespace E2E.Tests.Services.Missions;

public partial class MissileOutcomeTests
{
    [Theory]
    [InlineData(WeaponClass.Javelin, (WeaponFlags)0)]
    [InlineData(WeaponClass.Arrow, WeaponFlags.AmmoBreaksOnBounceBack)]
    public void DebugForceBounce_UsesVanillaCollisionAndSendsOnlyAtSettlement(WeaponClass weaponClass, WeaponFlags breakFlags)
    {
        RunClients((shooter, peer, native) =>
        {
            native.EnableDebugBounceCallback();
            shooter.Instance.Call(() =>
            {
                var usage = shooter.Weapon.Item.PrimaryWeapon;
                usage.WeaponClass = weaponClass;
                usage.WeaponFlags |= breakFlags;
            });
            peer.Receive(shooter.Shoot());
            NetworkMissileOutcome outcome = null!;
            shooter.Instance.Call(() =>
            {
                var result = new Commands.ForceBounceMissilesCoopCommand().ProcessCommand(new CoopCommandArgsFactory().FromValues(new[] { "1" }));
                Assert.True(result.Succeeded, result.Output);
                Assert.Contains("\"remaining\":1", result.Output);
                var missile = Assert.Single(shooter.Mission.Shell._missilesDictionary).Value;
                var parent = native.AddParent(shooter, 37, new MatrixFrame(Mat3.Identity, new Vec3(500f, 600f, 700f)));
                MatrixFrame impact = Frame(false);
                native.EntityFrames[missile.Entity.Pointer] = impact;
                var collision = BounceCollision(missile.Index, impact.origin);
                shooter.Mission.Shell.MissileHitCallback(out _, ref collision, Vec3.Zero, impact.origin,
                    Vec3.Zero, collision.MissileVelocity, impact, MatrixFrame.Identity, 0, shooter.Shooter, null,
                    native.EntityOf(parent.GameEntity));
                var spawn = Assert.Single(native.BounceSpawns);
                var usage = missile.Weapon.CurrentUsageItem;
                MatrixFrame expected = impact.TransformToParent(usage.GetMissileStartingFrame().TransformToParent(usage.StickingFrame))
                    .TransformToParent(usage.GetMissileStartingFrame());
                AssertFrame(expected, spawn.Frame, exact: false);
                Assert.Null(spawn.Parent);
                Assert.True(spawn.Velocity.LengthSquared > 0f && !float.IsInfinity(spawn.Velocity.LengthSquared));
                Assert.True(spawn.AngularVelocity.LengthSquared > 0f && !float.IsInfinity(spawn.AngularVelocity.LengthSquared));
                output.WriteLine($"forced bounce global={spawn.Frame.origin} velocity={spawn.Velocity} angularVelocity={spawn.AngularVelocity}");
                var item = Assert.Single(shooter.Items.GetAll()).Value;
                Assert.True(item.SpawnFlags.HasAnyFlag(Mission.WeaponSpawnFlags.WithPhysics));
                Assert.False(item.SpawnFlags.HasAnyFlag(Mission.WeaponSpawnFlags.WithStaticPhysics));
                Assert.Equal(0, shooter.Handler.ForcedBouncesRemaining);
                Assert.Empty(shooter.Network.NetworkSentMessages.GetMessages<NetworkMissileOutcome>());
                item.DisableDynamicBody();
                outcome = Assert.Single(shooter.Network.NetworkSentMessages.GetMessages<NetworkMissileOutcome>());
                Assert.Equal(MissileOutcomeKind.BounceSettled, outcome.Kind);
            });
            peer.Receive(outcome);
            peer.Instance.Call(() =>
            {
                peer.Handler.DrainPendingShots();
                var item = Assert.Single(peer.Items.GetAll()).Value;
                Assert.True(item.PhysicsStopped);
                Assert.True((Frame(false).origin - item.GameEntity.GetGlobalFrame().origin).Length < 0.001f);
            });
        }, realParenting: true);
    }

    private static AttackCollisionData BounceCollision(int index, Vec3 position) =>
        AttackCollisionData.GetAttackCollisionDataForDebugPurpose(false, false, false, false, false, true,
            false, false, true, false, false, false, default, index, 0, 0, -1, default, -1, default, -1,
            default, 0f, 0f, 0f, 0f, 0f, 50f, 0f, 0f, Vec3.Up, Vec3.Forward, position,
            new Vec3(10f, 0f, -20f), Vec3.Zero, Vec3.Zero, Vec3.Up);

    [Fact]
    public void DebugFireAt_SkipsMountsAndUnarmedAgentsAndReplicatesHumanShots()
    {
        RunClients((shooter, peer, native) =>
        {
            native.EnableDebugAgentShots();
            shooter.Instance.Call(() =>
            {
                shooter.Shooter.Equipment[EquipmentIndex.Weapon0] = shooter.Weapon;
                shooter.Shooter.SetWieldedItemIndexAsClient(Agent.HandIndex.MainHand, EquipmentIndex.Weapon0, true, false, 0);
                var agents = shooter.Instance.Resolve<INetworkAgentRegistry>();
                Assert.True(agents.TryTransferAuthority("shooter", shooter.VictimId));
                var mount = shooter.Mission.SpawnMount();
                Assert.True(AgentMirror.TryGet(mount, out var mountMirror));
                mountMirror.IsHuman = false;
                Assert.True(agents.TryRegisterAgent("shooter", Guid.NewGuid(), mount));
                var noEquipment = shooter.Mission.SpawnAgent(new AgentBuildData(Game.Current.PlayerTroop).Controller(AgentControllerType.None));
                Assert.True(AgentMirror.TryGet(noEquipment, out var incomplete));
                incomplete.Equipment = null!;
                Assert.True(agents.TryRegisterAgent("shooter", Guid.NewGuid(), noEquipment));
                native.UnsafeShooterReads.UnionWith(new[] { mount, shooter.Victim, noEquipment });
                native.AddParent(shooter, 37, new MatrixFrame(Mat3.Identity, new Vec3(10f, 20f, 2f)));
                var result = new Commands.FireMissilesAtCoopCommand().ProcessCommand(new CoopCommandArgsFactory().FromValues(new[] { "37", "3" }));
                Assert.True(result.Succeeded, result.Output);
                var json = JObject.Parse(result.Output.Substring("LIVE_TEST_JSON=".Length));
                Assert.Equal(3, (int)json["missiles"]!);
                Assert.Single((JArray)json["shooters"]!);
                Assert.Equal(3, shooter.Mission.Shell._missilesDictionary.Count);
                shooter.Shooter.Equipment[EquipmentIndex.Weapon0] = MissionWeapon.Invalid;
                native.UnsafeShooterReads.Add(shooter.Shooter);
                var noShooter = new Commands.FireMissilesAtCoopCommand().ProcessCommand(
                    new CoopCommandArgsFactory().FromValues(new[] { "37", "1" }));
                Assert.False(noShooter.Succeeded);
                Assert.Equal("no_shooter", noShooter.ErrorCode);
            });
            var shots = shooter.Network.NetworkSentMessages.GetMessages<NetworkAgentShoot>().ToArray();
            Assert.Equal(3, shots.Length);
            foreach (var shot in shots) peer.Receive(shot);
            peer.Instance.Call(() =>
            {
                peer.Handler.DrainPendingShots();
                Assert.Equal(3, peer.Mission.Shell._missilesDictionary.Count);
                Assert.All(peer.Mission.Shell._missilesDictionary.Values, value => Assert.Same(peer.Shooter, value.ShooterAgent));
            });
        });
    }

    [Fact]
    public void DebugDestructibles_ReportsTheLiveStateWithoutChangingIt()
    {
        RunClients((shooter, peer, native) =>
        {
            shooter.Instance.Call(() =>
            {
                var frame = new MatrixFrame(Mat3.Identity, new Vec3(10f, 20f, 30f));
                var destruction = native.AddDestructible(shooter, 37, frame, out _, out var broken);
                for (int i = 1; i < 70; i++)
                    native.AddDestructible(shooter, 37 + i, frame, out _, out _).HitPoint = 100f;
                destruction.HitPoint = 65f;
                destruction.SetDestructionLevel(1, -1, 5f, Vec3.Zero, Vec3.Up, noEffects: true);
                var result = new Commands.MissileDestructiblesCoopCommand().ProcessCommand(new CoopCommandArgsFactory().FromValues(Array.Empty<string>()));
                Assert.True(result.Succeeded, result.Output);
                var json = JObject.Parse(result.Output.Substring("LIVE_TEST_JSON=".Length));
                var rows = (JArray)json["rows"]!;
                Assert.Equal(16, rows.Count);
                Assert.Equal(70, (int)json["total"]!);
                var row = rows[0];
                Assert.Equal(37, (int)row["missionObjectId"]!);
                Assert.Equal(nameof(DestructableComponent), (string)row["type"]!);
                Assert.Equal(1, (int)row["state"]!);
                Assert.Equal(65f, (float)row["hitPoints"]!);
                Assert.Equal(10f, (float)row["position"]!["x"]!);
                Assert.Same(broken, destruction.CurrentState);
                Assert.Equal(65f, destruction.HitPoint);
                var last = new Commands.MissileDestructiblesCoopCommand().ProcessCommand(
                    new CoopCommandArgsFactory().FromValues(new[] { "64", "64" }));
                Assert.True(last.Succeeded, last.Output);
                var page = JObject.Parse(last.Output.Substring("LIVE_TEST_JSON=".Length));
                Assert.Equal(70, (int)page["total"]!);
                Assert.Equal(6, (int)page["count"]!);
                Assert.Equal(101, (int)page["rows"]![0]!["missionObjectId"]!);
                Assert.Equal(new[] { "missionObjectId", "type", "state", "hitPoints", "position" },
                    ((JObject)page["rows"]![0]!).Properties().Select(value => value.Name));
            });
        }, realParenting: true);
    }

    [Fact]
    public void DebugDestroy_RunsVanillaHitAndReplicatesItsDestructionState()
    {
        RunClients((shooter, peer, native) =>
        {
            native.EnableDebugSiege();
            DestructableComponent source = null!, target = null!;
            shooter.Instance.Call(() => source = native.AddDebugMachine(shooter, 47));
            peer.Instance.Call(() => target = native.AddDebugMachine(peer, 47));
            using var sourceState = DebugSiegeState(shooter, true);
            using var targetState = DebugSiegeState(peer, false);
            bool previousHost = SiegeMissionAuthorityGate.IsLocalAuthority;
            bool previousKnown = SiegeMissionAuthorityGate.IsAuthorityKnown;
            try
            {
                NetworkSiegeMachineState state = null!;
                shooter.Instance.Call(() =>
                {
                    SiegeMissionAuthorityGate.IsLocalAuthority = true;
                    SiegeMissionAuthorityGate.IsAuthorityKnown = true;
                    var result = new Commands.DestroyMissileParentCoopCommand().ProcessCommand(new CoopCommandArgsFactory().FromValues(new[] { "47" }));
                    Assert.True(result.Succeeded, result.Output);
                    Assert.Contains("\"before\":0,\"after\":1", result.Output);
                    Assert.True(source.IsDestroyed);
                    AccessTools.Method(typeof(SiegeMachineStateReplicator), "RefreshMachineCache").Invoke(sourceState, null);
                    AccessTools.Method(typeof(SiegeMachineStateReplicator), "BroadcastChangedStates").Invoke(sourceState, null);
                    state = Assert.Single(shooter.Network.NetworkSentMessages.GetMessages<NetworkSiegeMachineState>());
                });
                peer.Instance.Call(() =>
                {
                    SiegeMissionAuthorityGate.IsLocalAuthority = false;
                    SiegeMissionAuthorityGate.IsAuthorityKnown = true;
                    peer.Receive(state);
                    Assert.True(target.IsDestroyed);
                    Assert.Equal(1, target._currentStateIndex);
                    Assert.Equal(source.HitPoint, target.HitPoint);
                });
            }
            finally
            {
                SiegeMissionAuthorityGate.IsLocalAuthority = previousHost;
                SiegeMissionAuthorityGate.IsAuthorityKnown = previousKnown;
                SiegeMissionAuthorityGate.ResetClaimedMachines();
            }
        }, realParenting: true);
    }

    private static SiegeMachineStateReplicator DebugSiegeState(Client client, bool host)
    {
        var session = new Mock<IBattleSession>();
        session.SetupGet(value => value.IsLocalHost).Returns(host);
        session.SetupGet(value => value.OwnControllerId).Returns(host ? "shooter" : "peer");
        session.SetupGet(value => value.HostControllerId).Returns("shooter");
        session.SetupGet(value => value.HostEpoch).Returns(1);
        session.Setup(value => value.IsHostController("shooter")).Returns(true);
        var state = new SiegeMachineStateReplicator(client.Network, client.Broker, session.Object,
            client.Instance.Resolve<INetworkAgentRegistry>(), new HostEpochPolicy(), new RangedSiegeWeaponPresentation());
        var controller = client.Mission.Shell.GetMissionBehavior<CoopBattleController>();
        AccessTools.Field(typeof(CoopBattleController), "<Session>k__BackingField").SetValue(controller, session.Object);
        AccessTools.Field(typeof(CoopBattleController), "siegeMachineState").SetValue(controller, state);
        return state;
    }

    [Fact]
    public void DebugStuck_ReportsTheOldParentAndRoundTripFrameAfterDestruction()
    {
        RunClients((shooter, peer, native) =>
        {
            shooter.Shoot();
            shooter.Instance.Call(() =>
            {
                var parent = native.AddDestructible(shooter, 37, MatrixFrame.Identity, out var original, out _);
                shooter.Contact(Assert.Single(shooter.Mission.Shell._missilesDictionary).Value,
                    Mission.MissileCollisionReaction.Stick, Frame(true), parent: parent);
                parent.SetDestructionLevel(1, -1, 5f, Vec3.Zero, Vec3.Up, noEffects: true);
                var item = Assert.Single(shooter.Items.GetAll());
                var result = new Commands.StuckMissilesCoopCommand(shooter.Items).ProcessCommand(new CoopCommandArgsFactory().FromValues(new[] { "0", "64" }));
                Assert.True(result.Succeeded, result.Output);
                Assert.StartsWith("STUCK_MISSILES total=1 offset=0\nSTUCK_MISSILE worldItem=" + item.Key, result.Output);
                Assert.Contains("item=outcome_javelin amount=1", result.Output);
                Assert.Contains("o=" + Frame(true).origin.x.ToString("R", CultureInfo.InvariantCulture), result.Output);
                Assert.Contains("parent=entity", result.Output);
                Assert.Same(original, native.ParentOf(item.Value.GameEntity));
            });
        }, realParenting: true);
    }

    private sealed partial class NativeFixture
    {
        private MissionGameModels? previousBounceModels;
        private bool bounceModelsInstalled;
        private FieldInfo? bounceVoiceBridge;
        private object? previousBounceVoices;
        public sealed record BounceSpawn(MatrixFrame Frame, GameEntity? Parent, Vec3 Velocity, Vec3 AngularVelocity);
        public List<BounceSpawn> BounceSpawns { get; } = new();
        public HashSet<Agent> UnsafeShooterReads { get; } = new();

        public void EnableDebugBounceCallback()
        {
            previousBounceModels = MissionGameModels.Current;
            _ = new MissionGameModels(new GameModel[] { Mock.Of<AgentApplyDamageModel>() });
            bounceModelsInstalled = true;
            bounceVoiceBridge = AccessTools.Field(typeof(MBAPI), "IMBVoiceManager");
            previousBounceVoices = bounceVoiceBridge.GetValue(null);
            object voices = typeof(DispatchProxy).GetMethod(nameof(DispatchProxy.Create), Type.EmptyTypes)!
                .MakeGenericMethod(bounceVoiceBridge.FieldType, typeof(EntityEngine)).Invoke(null, null)!;
            var voiceIds = new Dictionary<string, int>();
            ((EntityEngine)voices).Call = (method, args) =>
            {
                if (method.Name != "GetVoiceTypeIndex") throw new InvalidOperationException("Unexpected voice call: " + method.Name);
                string id = (string)args[0]!;
                if (!voiceIds.TryGetValue(id, out int index)) voiceIds.Add(id, index = voiceIds.Count);
                return index;
            };
            bounceVoiceBridge.SetValue(null, voices);
            Patch(typeof(ModuleHelper), nameof(ModuleHelper.GetModuleFullPath), nameof(DebugModulePath));
            Patch(typeof(PhysicsMaterial), nameof(PhysicsMaterial.GetFromName), nameof(DebugMaterial));
            Patch(typeof(ParticleSystemManager), nameof(ParticleSystemManager.GetRuntimeIdByName), nameof(Skip));
            Patch(typeof(ManagedParameters), nameof(ManagedParameters.GetManagedParameter), nameof(DebugManagedParameter));
            Patch(typeof(Mission), "GetAttackCollisionResults", nameof(DebugAttackResults));
            Patch(typeof(Mission), "CreateMissileBlow", nameof(DebugMissileBlow));
            Patch(typeof(Mission), nameof(Mission.RegisterBlow), nameof(Skip));
            Patch(typeof(MBMissile), nameof(MBMissile.GetPosition), nameof(DebugMissilePosition));
            Patch(typeof(Mission), "AddSoundAlarmFactorToAgents", nameof(Skip));
            harmony.Patch(AccessTools.Method(typeof(Mission), "SpawnWeaponAux"),
                postfix: new HarmonyMethod(typeof(NativeFixture), nameof(DebugBounceSpawn)));
            harmony.Patch(AccessTools.Method(typeof(Mission), nameof(Mission.MissileHitCallback)),
                postfix: Method(nameof(RebuildCollision)));
        }

        private static void DebugBounceSpawn(WeakGameEntity weaponEntity, Vec3 globalVelocity, Vec3 globalAngularVelocity)
        {
            current.BounceSpawns.Add(new BounceSpawn(weaponEntity.GetGlobalFrame(), current.ParentOf(weaponEntity),
                globalVelocity, globalAngularVelocity));
        }
        private static bool DebugMaterial(ref PhysicsMaterial __result) { __result = PhysicsMaterial.InvalidPhysicsMaterial; return false; }
        private static bool DebugModulePath(string __0, ref string __result)
        {
            if (__0 != "Native") return true;
            __result = System.IO.Path.Combine(System.Environment.GetEnvironmentVariable("BANNERLORD_DIR")!, "Modules", "Native")
                + System.IO.Path.DirectorySeparatorChar;
            return false;
        }
        private static bool DebugManagedParameter(ManagedParametersEnum managedParameterEnum, ref float __result)
        {
            __result = managedParameterEnum == ManagedParametersEnum.ProjectileMaxPenetrationSpeed ? 50f : 0f;
            return false;
        }
        private static bool DebugAttackResults(out WeaponComponentData shieldOnBack, out CombatLogData combatLog)
        {
            shieldOnBack = null!;
            combatLog = default;
            return false;
        }
        private static bool DebugMissileBlow(ref Blow __result) { __result = default; return false; }
        private static bool DebugMissilePosition(MBMissile __instance, ref Vec3 __result)
        {
            __result = ((Mission.Missile)__instance).Entity.GetGlobalFrame().origin;
            return false;
        }
        public void EnableDebugAgentShots()
        {
            Patch(typeof(Agent), nameof(Agent.GetPrimaryWieldedItemIndex), nameof(DebugWieldedIndex));
            var add = AccessTools.Method(typeof(Mission), nameof(Mission.AddMissileSingleUsageAux));
            harmony.Unpatch(add, HarmonyPatchType.Prefix, harmony.Id);
            // Capture the real wielded weapon before the terminal native-add stub returns.
            harmony.Patch(add, prefix: new HarmonyMethod(typeof(NativeFixture), nameof(AddMissile)) { priority = Priority.Last });
            Patch(typeof(Agent), nameof(Agent.GetEyeGlobalPosition), nameof(DebugEye));
            Patch(typeof(MissionWeapon), nameof(MissionWeapon.GetModifiedMissileSpeedForCurrentUsage), nameof(DebugSpeed));
            Patch(typeof(ItemObject), nameof(ItemObject.GetAirFrictionConstant), nameof(DebugTime));
            Patch(typeof(Mission), nameof(Mission.GetMissileVerticalAimCorrection), nameof(DebugAim));
            Patch(typeof(Agent), nameof(Agent.UpdateLastRangedAttackTimeDueToAnAttack), nameof(Skip));
            Patch(typeof(MBCommon), nameof(MBCommon.GetTotalMissionTime), nameof(DebugTime));
            foreach (var patch in new[] { typeof(BlockMissileIfNative), typeof(AddMissileSingleUsageAuxPatch) })
            {
                var target = patch == typeof(BlockMissileIfNative)
                    ? AccessTools.Method(typeof(Mission), nameof(Mission.OnAgentShootMissile))
                    : AccessTools.Method(typeof(Mission), nameof(Mission.AddMissileSingleUsageAux));
                var existing = Harmony.GetPatchInfo(target);
                if (existing == null || !existing.Prefixes.Concat(existing.Postfixes).Any(value => value.PatchMethod.DeclaringType == patch))
                    harmony.CreateClassProcessor(patch).Patch();
            }
            harmony.Patch(AccessTools.Method(typeof(Mission), nameof(Mission.OnAgentShootMissile)), postfix: Method(nameof(RebuildCollision)));
        }

        public void EnableDebugSiege()
        {
            Patch(typeof(Mission), "get_IsSiegeBattle", nameof(DebugSiege));
            Patch(typeof(Mission), nameof(Mission.OnObjectDisabled), nameof(Skip));
        }

        public DestructableComponent AddDebugMachine(Client client, int id)
        {
            var destruction = AddDestructible(client, id + 1, MatrixFrame.Identity, out _, out _);
            destruction.HitPoint = destruction.MaxHitPoint = 100f;
            destruction.HeavyHitParticlesThreshold = 1000f;
            destruction.DestructionStates = "broken";
            destruction.ParticleEffectOnDestroy = destruction.SoundEffectOnDestroy = "";
            var machine = ObjectHelper.SkipConstructor<StonePile>();
            machine.Id = new MissionObjectId(id, false);
            machine._gameEntity = destruction.GameEntity;
            machine.DestructionComponent = destruction;
            client.Mission.Shell._missionObjects.Add(machine);
            return destruction;
        }

        private static bool DebugEye(Agent __instance, ref Vec3 __result) { __result = __instance.Position + Vec3.Up * 1.5f; return false; }
        private static void DebugWieldedIndex(Agent __instance)
        {
            if (current.UnsafeShooterReads.Contains(__instance))
                throw new NullReferenceException("Native wielded-index access on an agent without valid shooter equipment.");
        }
        private static bool DebugSpeed(ref int __result) { __result = 50; return false; }
        private static bool DebugTime(ref float __result) { __result = 0f; return false; }
        private static bool DebugAim(Vec3 vecToTarget, ref float __result)
        {
            __result = (float)Math.Atan2(vecToTarget.z, Math.Sqrt((vecToTarget.x * vecToTarget.x) + (vecToTarget.y * vecToTarget.y)));
            return false;
        }
        private static bool DebugSiege(ref bool __result) { __result = true; return false; }
    }
}
#endif
