using Common.Messaging;
using Common.PacketHandlers;
using Common.Serialization;
using Common.Util;
using E2E.Tests.Environment.Instance;
using E2E.Tests.Environment.Mock;
using E2E.Tests.Environment.MockEngine;
using GameInterface.Surrogates;
using HarmonyLib;
using Missions;
using Missions.Missiles;
using Missions.Missiles.Handlers;
using Missions.Missiles.Message;
using TaleWorlds.Core;
using TaleWorlds.Engine;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
using TaleWorlds.ObjectSystem;
using Xunit.Abstractions;

namespace E2E.Tests.Services.Missions;

/// <summary>Fires a shot through the shooter's <see cref="MissileHandler"/>, the wire, and the peer's replay.</summary>
public class MissileShotReplayTests : MissionTestEnvironment
{
    // A real three-layer clan banner (clan_aserai_6 in spclans.xml).
    internal const string ClanBannerCode =
        "11.156.75.4345.4345.764.764.1.0.0.222.1.0.454.454.764.764.0.0.359.411.1.0.220.220.764.764.0.0.0";

    private static readonly List<NativeLaunch> NativeLaunches = new();
    private static readonly List<MissionWeapon> ReplayedWeapons = new();

    public MissileShotReplayTests(ITestOutputHelper output) : base(output) { }

    public static IEnumerable<object[]> Shots()
    {
        // Launcher class (none when the missile is thrown), missile class, tableau, rigid body, skewed launch frame.
        yield return new object[] { WeaponClass.Bow, WeaponClass.Arrow, false, false, false };
        yield return new object[] { WeaponClass.Crossbow, WeaponClass.Bolt, false, false, false };
        yield return new object[] { WeaponClass.Undefined, WeaponClass.Javelin, false, false, false };
        yield return new object[] { WeaponClass.Undefined, WeaponClass.ThrowingAxe, false, true, false };
        yield return new object[] { WeaponClass.Undefined, WeaponClass.Stone, false, true, false };
        // Vanilla has no throwable tableau item, but a modded one must keep its banner.
        yield return new object[] { WeaponClass.Undefined, WeaponClass.ThrowingAxe, true, true, false };
        // A frame that isn't a rotation doesn't pack, so it travels exactly and the replay launches with it.
        yield return new object[] { WeaponClass.Bow, WeaponClass.Arrow, false, false, true };
    }

    [Theory]
    [MemberData(nameof(Shots))]
    public void Shot_ReplaysOnThePeerWithTheShooterLaunch(
        WeaponClass launcherClass, WeaponClass missileClass, bool usingTableau, bool hasRigidBody, bool skewedFrame)
    {
        using var fixture = new MissionEngineFixture();
        var harmony = new Harmony("e2e.missile-shot-replay." + Guid.NewGuid().ToString("N"));
        PatchNativeMissileAdd(harmony);
        NativeLaunches.Clear();
        ReplayedWeapons.Clear();

        EnvironmentInstance shooterClient = Clients.First();
        EnvironmentInstance peerClient = Clients.Skip(1).First();
        SetControllerId(shooterClient, "shooter");
        SetControllerId(peerClient, "peer");
        _ = new SurrogateCollection();

        ItemObject missileItem = CreateItem("missile_" + missileClass, missileClass,
            WeaponFlags.RangedWeapon | WeaponFlags.Consumable, usingTableau);
        ItemObject? launcherItem = launcherClass == WeaponClass.Undefined
            ? null
            : CreateItem("launcher_" + launcherClass, launcherClass, WeaponFlags.RangedWeapon, false);
        var banner = new Banner(ClanBannerCode);
        var missile = new MissionWeapon(missileItem, null, banner);
        MissionWeapon wielded = launcherItem == null
            ? missile
            : new MissionWeapon(launcherItem, null, banner, 0, 0, missile);

        Vec3 position = new Vec3(120.5f, 340.25f, 12.75f);
        Vec3 direction = new Vec3(0.8f, 0.55f, 0.24f).NormalizedCopy();
        Mat3 orientation = Mat3.Identity;
        orientation.RotateAboutUp(0.6f);
        orientation.RotateAboutSide(0.25f);
        orientation.RotateAboutForward(hasRigidBody ? 1.3f : 0f);
        if (skewedFrame)
            orientation = new Mat3(new Vec3(1f, 0.2f, 0f), new Vec3(0.1f, 1.5f, 0f), new Vec3(0f, 0f, 0.7f));
        var agentId = Guid.NewGuid();

        try
        {
            NetworkAgentShoot? sent = null;
            shooterClient.Call(() =>
            {
                MockMission mission = fixture.CreateMission(shooterClient);
                Agent shooter = mission.SpawnAgent(
                    new AgentBuildData(Game.Current.PlayerTroop).Controller(AgentControllerType.None));
                Assert.True(shooterClient.Resolve<INetworkAgentRegistry>().TryRegisterAgent("shooter", agentId, shooter));
                var network = shooterClient.Resolve<MockBattleNetwork>();
                network.RouteMessages = false;
                using var broker = new MessageBroker();
                using var handler = new MissileHandler(network, broker,
                    shooterClient.Resolve<INetworkAgentRegistry>(), new MissileOrientationCodec());

                broker.Publish(this, new AgentShoot(shooter, wielded, position, direction, orientation,
                    55f, 61f, hasRigidBody, missileIndex: 7));

                sent = Assert.Single(network.NetworkSentMessages.GetMessages<NetworkAgentShoot>());
            });

            var serializer = new ProtoBufSerializer(new SerializableTypeMapper());
            var received = Assert.IsType<NetworkAgentShoot>(
                serializer.Deserialize<IMessage>(MessagePacket.Create(sent!, serializer).Data));
            Assert.Equal(usingTableau ? ClanBannerCode : null, received.Banner?.Serialize());
            Assert.Equal(skewedFrame, received.ExactOrientation.HasValue);
            if (skewedFrame)
                Assert.Equal(0UL, received.PackedOrientation);

            peerClient.Call(() =>
            {
                MBObjectManager.Instance.RegisterObject(missileItem);
                MockMission mission = fixture.CreateMission(peerClient);
                Agent shooterCopy = mission.SpawnAgent(
                    new AgentBuildData(Game.Current.PlayerTroop).Controller(AgentControllerType.None));
                var registry = peerClient.Resolve<INetworkAgentRegistry>();
                Assert.True(registry.TryRegisterAgent("shooter", agentId, shooterCopy));
                using var broker = new MessageBroker();
                using var handler = new MissileHandler(peerClient.Resolve<MockBattleNetwork>(), broker, registry,
                    new MissileOrientationCodec());

                broker.Publish(this, received);
                handler.DrainPendingShots();

                NativeLaunch launch = Assert.Single(NativeLaunches);
                Assert.Same(shooterCopy, launch.Shooter);
                Assert.Equal(position, launch.Position);
                Assert.Equal(direction, launch.Direction);
                Assert.Equal(61f, launch.Speed);
                Assert.Equal(55f, launch.BaseSpeed);
                Assert.Equal(hasRigidBody, launch.HasRigidBody);
                if (skewedFrame)
                    Assert.Equal(orientation, launch.Orientation);
                else
                    AssertWithinTolerance(orientation, launch.Orientation);
                MissionWeapon replayed = Assert.Single(ReplayedWeapons);
                Assert.Same(missileItem, replayed.Item);
                Assert.Equal(usingTableau ? ClanBannerCode : null, replayed.Banner?.Serialize());
                Assert.False(handler.IsReconstructionPending(agentId, received.ShotSequence));
            });
        }
        finally
        {
            harmony.UnpatchAll(harmony.Id);
            NativeLaunches.Clear();
            ReplayedWeapons.Clear();
        }
    }

    internal static void AssertWithinTolerance(Mat3 expected, Mat3 actual)
    {
        Assert.True(expected.NearlyEquals(actual, MissileOrientationCodec.Tolerance),
            $"expected {expected} but the replay launched with {actual}");
    }

    internal static ItemObject CreateItem(string stringId, WeaponClass weaponClass, WeaponFlags flags,
        bool usingTableau)
    {
        // The registry's StringId setter patch lets only allowed threads name an object.
        using var allowed = new AllowedThread();
        var item = new ItemObject { StringId = stringId, IsUsingTableau = usingTableau };
        item.AddWeapon(new WeaponComponentData(item, weaponClass, flags), null);
        return item;
    }

    private static void PatchNativeMissileAdd(Harmony harmony)
    {
        harmony.Patch(AccessTools.Method(typeof(MissionWeapon), nameof(MissionWeapon.GetWeaponData)),
            prefix: new HarmonyMethod(typeof(MissileShotReplayTests), nameof(SkipWeaponData)));
        harmony.Patch(AccessTools.Method(typeof(MissionWeapon), nameof(MissionWeapon.GetWeaponStatsDataForUsage)),
            prefix: new HarmonyMethod(typeof(MissileShotReplayTests), nameof(SkipWeaponStatsData)));
        harmony.Patch(AccessTools.Method(typeof(Mission), "AddMissileSingleUsageAux"),
            prefix: new HarmonyMethod(typeof(MissileShotReplayTests), nameof(CaptureNativeAdd)));
        harmony.Patch(AccessTools.Method(typeof(Mission), "AddMissileAux"),
            prefix: new HarmonyMethod(typeof(MissileShotReplayTests), nameof(CaptureNativeAdd)));
    }

    // Weapon meshes, sounds and physics materials are native lookups the replay only hands on to the engine.
    private static bool SkipWeaponData(MissionWeapon __instance, ref WeaponData __result)
    {
        ReplayedWeapons.Add(__instance);
        __result = default;
        return false;
    }

    private static bool SkipWeaponStatsData(ref WeaponStatsData __result)
    {
        __result = default;
        return false;
    }

    private static bool CaptureNativeAdd(Agent shooterAgent, ref Vec3 position, ref Vec3 direction,
        ref Mat3 orientation, float baseSpeed, float speed, bool addRigidBody, out GameEntity? missileEntity,
        ref int __result)
    {
        NativeLaunches.Add(new NativeLaunch(shooterAgent, position, direction, orientation, baseSpeed, speed,
            addRigidBody));
        // A native entity can't be built headless, so the replay stops right after the native add.
        missileEntity = null;
        __result = NativeLaunches.Count;
        return false;
    }

    private sealed record NativeLaunch(Agent Shooter, Vec3 Position, Vec3 Direction, Mat3 Orientation,
        float BaseSpeed, float Speed, bool HasRigidBody);
}
