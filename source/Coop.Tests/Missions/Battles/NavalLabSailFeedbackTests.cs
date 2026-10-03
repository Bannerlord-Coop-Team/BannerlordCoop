#if DEBUG
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using HarmonyLib;
using Missions.Battles;
using Missions.Messages;
using Missions.Naval;
using Moq;
using NavalDLC.GauntletUI.MissionViews;
using NavalDLC.Missions;
using NavalDLC.Missions.Objects;
using NavalDLC.Missions.ShipActuators;
using NavalDLC.Missions.ShipControl;
using NavalDLC.Missions.ShipInput;
using NavalDLC.ViewModelCollection.Missions.ShipControl;
using TaleWorlds.Core;
using TaleWorlds.Engine;
using TaleWorlds.Engine.GauntletUI;
using TaleWorlds.GauntletUI;
using TaleWorlds.GauntletUI.GamepadNavigation;
using TaleWorlds.GauntletUI.BaseTypes;
using TaleWorlds.InputSystem;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
using TaleWorlds.TwoDimension;
using Xunit;
using static Coop.Tests.Missions.Battles.NavalLabTestShells;

namespace Coop.Tests.Missions.Battles;

[Collection("Mission.Current")]
public sealed class NavalLabSailFeedbackTests : IDisposable
{
    private readonly Harmony harmony = new("coop.tests.naval.sail-feedback");
    private readonly MissionCurrentScope scope = new();
    private static readonly Dictionary<MissionShip, MBReadOnlyList<MissionSail>> sails = new();
    private static Scene scene = null!;
    private readonly NavalLabBehavior fixture;
    private readonly MissionGauntletShipControlView view;
    private readonly MissionShipControlVM vm;
    private readonly NavalLabManifest manifest;

    public NavalLabSailFeedbackTests()
    {
        sails.Clear();
        Patch(AccessTools.Method(typeof(SoundEvent), nameof(SoundEvent.GetEventIdFromString)), nameof(Zero));
        Patch(AccessTools.Method(typeof(MBAnimation), nameof(MBAnimation.GetActionCodeWithName)), nameof(Zero));
        Patch(AccessTools.PropertyGetter(typeof(MissionShip), nameof(MissionShip.Sails)), nameof(Sails));
        Patch(AccessTools.Method(typeof(NavalLabBehavior), "HasNativeOwnerIdentity"), nameof(True));
        Patch(AccessTools.Method(typeof(NavalLabBehavior), "GetLocalControlledShip"), nameof(Controlled));
        Patch(AccessTools.PropertyGetter(typeof(Mission), nameof(Mission.Scene)), nameof(SceneValue));
        Patch(AccessTools.Method(typeof(Scene), nameof(Scene.GetGlobalWindStrengthVector)), nameof(Wind));
        Patch(AccessTools.PropertyGetter(typeof(MissionShip), nameof(MissionShip.GlobalFrame)), nameof(Frame));
        // HP and order UI are unrelated to the actual sail reader/VM and require a native mission.
        Patch(AccessTools.Method(typeof(MissionGauntletShipControlView), "UpdateHitPoints"), nameof(Skip));
        Patch(AccessTools.Method(typeof(ShipOrder), nameof(ShipOrder.GetIsCuttingLoose)), nameof(False));
        Patch(AccessTools.Method(typeof(ShipOrder), nameof(ShipOrder.GetIsAttemptingBoarding)), nameof(False));
        Patch(AccessTools.Method(typeof(PlayerShipController), nameof(PlayerShipController.SetInput)), nameof(ForbiddenWrite));
        scene = SceneAtEngineBoundary();
        var id = Guid.NewGuid();
        manifest = new NavalLabManifest("naval-lab:" + id.ToString("N"), id, new[] { "A", "B" },
            Enumerable.Range(0, 10).Select(_ => Guid.NewGuid()).ToArray(), new[] { Guid.NewGuid(), Guid.NewGuid() }, NavalLabMode.TwoClientNative);
        fixture = new NavalLabBehavior(manifest, "B", null!, null!);
        Bind(fixture);
        fixture.Ships = new[] { Ship(1), Ship(0) };
        fixture.nativeDeploymentComplete = true;
        fixture.NativeAuthority = () => true;
        view = Shell<MissionGauntletShipControlView>(); Bind(view);
        vm = Shell<MissionShipControlVM>();
        vm.SetSailState((SailInput)(-1));
        Set(view, "_dataSource", vm); Set(view, "_playerControlledShip", fixture.LocalShip);
        Set(view, "SailControl", SailInput.Full);
        NavalLabPhysicsPatches.Active = fixture;
    }
    private void Bind(MissionBehavior behavior) => AccessTools.PropertySetter(typeof(MissionBehavior), nameof(MissionBehavior.Mission)).Invoke(behavior, new object[] { scope.Instance });
    private void Patch(MethodBase method, string prefix) => harmony.Patch(method, prefix: new HarmonyMethod(GetType(), prefix));
    private static bool Zero(ref int __result) { __result = 0; return false; }
    private static bool True(ref bool __result) { __result = true; return false; }
    private static bool False(ref bool __result) { __result = false; return false; }
    private static bool Skip() => false;
    private static bool ForbiddenWrite() => throw new InvalidOperationException("presentation wrote a controller");
    private static bool Controlled(NavalLabBehavior __instance, ref MissionShip __result) { __result = __instance.LocalShip; return false; }
    private static bool SceneValue(ref Scene __result) { __result = scene; return false; }
    private static bool Wind(ref Vec2 __result) { __result = new Vec2(1, 0); return false; }
    private static bool Frame(ref MatrixFrame __result) { __result = MatrixFrame.Identity; return false; }
    private static bool Sails(MissionShip __instance, ref MBReadOnlyList<MissionSail> __result) { __result = sails[__instance]; return false; }
    private static MissionShip Ship(float target)
    {
        var ship = Shell<MissionShip>(); var sail = Shell<MissionSail>(); var definition = Shell<ShipSail>();
        Set(definition, "Type", SailType.Square);
        Set(sail, "_sailObject", definition); Set(sail, "<TargetSailSetting>k__BackingField", target);
        sails[ship] = new MBList<MissionSail> { sail };
        Set(ship, "<ShipOrder>k__BackingField", Shell<ShipOrder>());
        return ship;
    }
    private NetworkNavalLabFrames Feedback(long sequence = 1, int epoch = 1, Guid? incarnation = null,
        Guid? ship = null, long? deadline = null) => new(incarnation ?? manifest.IncarnationId, epoch, sequence, new float[24], 1,
            sailStates: new[] { new NetworkNavalLabSailState(manifest.Ships[0], 0, 0), new NetworkNavalLabSailState(ship ?? manifest.Ships[1], 2, 0) },
            sailDeadlineUtcTicks: deadline ?? DateTime.UtcNow.AddSeconds(1).Ticks);

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void HostOmitsUnavailableOrNonfiniteObservationInsteadOfPublishingCommandEcho(bool nonfinite)
    {
        fixture.factoryHost = true;
        if (nonfinite) Set(sails[fixture.LocalShip][0], "<TargetSailSetting>k__BackingField", float.NaN);
        else sails[fixture.LocalShip] = new MBList<MissionSail>();
        Assert.Null(fixture.ReadSailStates());
    }

    private static bool inputPermission;
    private static IInputContext nativeInput = null!;
    private static bool Permission(ref bool __result) { __result = inputPermission; return false; }
    private static bool InputValue(ref IInputContext __result) { __result = nativeInput; return false; }

    [Fact]
    public void SyntheticSailRequestUsesRealRouteAndConverters_WithoutEchoingObservationOrWritingController()
    {
        Set(scope.Instance, "<MissionBehaviors>k__BackingField", new List<MissionBehavior> { fixture, view });
        nativeInput = Mock.Of<IInputContext>();
        Patch(AccessTools.Method(typeof(NavalLabBehavior), "HasNativeInputPermission"), nameof(Permission));
        Patch(AccessTools.Method(typeof(MissionGauntletShipControlView), "GetCanToggleSail"), nameof(True));
        Patch(AccessTools.PropertyGetter(typeof(TaleWorlds.MountAndBlade.View.MissionViews.MissionView), "Input"), nameof(InputValue));
        var sent = new List<NetworkNavalLabHelmInput>(); fixture.SendNativeInput = sent.Add;
        inputPermission = false;
        Assert.StartsWith("rejected:", fixture.RequestSail(0)); Assert.Empty(sent);
        inputPermission = true;
        Assert.StartsWith("requested:synthetic", fixture.RequestSail(2));
        var input = Assert.Single(sent);
        Assert.Equal(1, input.Ship); Assert.Equal(manifest.IncarnationId, input.IncarnationId);
        Assert.True(input.HasHelm); Assert.Equal(2, input.Sail); Assert.Equal(0, input.Rudder);
        Assert.Equal(1, input.Sequence);
        Assert.Equal(0, sails[fixture.LocalShip][0].TargetSailSetting);
        Assert.StartsWith("rejected:", fixture.RequestSail(3)); Assert.Single(sent);
    }

    [Fact]
    public void SailFrameRoundTripPreservesIdentitySequenceAndDeadline_AbsentFeedbackIsCompatible()
    {
        var value = Feedback();
        using var stream = new System.IO.MemoryStream();
        ProtoBuf.Serializer.Serialize(stream, value); stream.Position = 0;
        var copy = ProtoBuf.Serializer.Deserialize<NetworkNavalLabFrames>(stream);
        Assert.Equal(value.IncarnationId, copy.IncarnationId); Assert.Equal(value.Sequence, copy.Sequence);
        Assert.Equal(value.SailDeadlineUtcTicks, copy.SailDeadlineUtcTicks);
        Assert.Equal(manifest.Ships[1], copy.SailStates[1].ShipId); Assert.Equal(2, copy.SailStates[1].State);
        stream.SetLength(0); stream.Position = 0;
        ProtoBuf.Serializer.Serialize(stream, new NetworkNavalLabFrames(manifest.IncarnationId, 1, 1, new float[24]));
        stream.Position = 0; Assert.Null(ProtoBuf.Serializer.Deserialize<NetworkNavalLabFrames>(stream).SailStates);
    }

    public void Dispose() { harmony.UnpatchAll(harmony.Id); NavalLabPhysicsPatches.Active = null; sails.Clear(); scope.Dispose(); }
}
#endif
