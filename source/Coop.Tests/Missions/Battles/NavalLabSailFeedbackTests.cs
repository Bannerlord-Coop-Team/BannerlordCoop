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

namespace Coop.Tests.Missions.Battles;

[Collection("Mission.Current")]
public sealed class NavalLabSailFeedbackTests : IDisposable
{
    private bool navigationCreated;
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
        scene = (Scene)AccessTools.Method(typeof(NavalLabSingleClientTeamAITests), "CreateSceneAtEngineBoundary").Invoke(null, null)!;
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
    private static void Set(object target, string field, object? value) => AccessTools.Field(target.GetType(), field).SetValue(target, value);
    private static T Shell<T>()
    {
        var managed = typeof(ScriptComponentBehavior).BaseType!.Assembly.GetType("TaleWorlds.DotNet.Managed")!;
        var field = AccessTools.Field(managed, "_moduleTypes"); var previous = field.GetValue(null);
        try { if (previous == null) field.SetValue(null, new Dictionary<string, Type>()); return (T)FormatterServices.GetUninitializedObject(typeof(T)); }
        finally { field.SetValue(null, previous); }
    }
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
    private void TickView() => AccessTools.Method(typeof(MissionGauntletShipControlView), "UpdateShipValues").Invoke(view, null);
    private void InstallPresentation()
    {
        var patch = typeof(NavalLabNativePatches).GetNestedType("OwnerSailPresentation", BindingFlags.NonPublic)!;
        harmony.Patch(AccessTools.Method(typeof(MissionGauntletShipControlView), "UpdateShipValues"),
            postfix: new HarmonyMethod(AccessTools.Method(patch, "Postfix")));
    }

    [Fact]
    public void RealViewReadsStaleLocalActuator_RealVmNowShowsHostFeedbackWithoutWrites()
    {
        vm.SetSailState(SailInput.Full);
        TickView(); Assert.Equal("Raised", vm.SailState);
        Assert.Equal(SailInput.Full, (SailInput)AccessTools.Field(view.GetType(), "SailControl").GetValue(view)!);
        fixture.factoryHost = true;
        var observed = fixture.ReadSailStates();
        Assert.Equal(2, observed[0].State); Assert.Equal(0, observed[1].State);
        fixture.factoryHost = false;
        InstallPresentation(); fixture.ApplySailFeedback(Feedback()); TickView();
        Assert.Equal("Full", vm.SailState); Assert.Equal(0, vm.SailType);
        TickView(); Assert.Equal("Full", vm.SailState);
        Assert.Equal(0, sails[fixture.LocalShip][0].TargetSailSetting);
        Assert.Equal(SailInput.Full, (SailInput)AccessTools.Field(view.GetType(), "SailControl").GetValue(view)!);
    }

    [Theory]
    [InlineData("epoch")]
    [InlineData("incarnation")]
    [InlineData("ship")]
    [InlineData("duplicate")]
    [InlineData("expired")]
    [InlineData("future")]
    [InlineData("missing")]
    [InlineData("not_ready")]
    [InlineData("terminal")]
    public void InvalidOrUnavailableFeedbackClearsPreviouslyAuthoritativeDisplay(string kind)
    {
        InstallPresentation(); fixture.ApplySailFeedback(Feedback()); TickView(); Assert.Equal("Full", vm.SailState);
        if (kind == "not_ready") fixture.NativeAuthority = () => false;
        if (kind == "terminal") fixture.factoryTerminal = true;
        var value = kind switch
        {
            "epoch" => Feedback(2, epoch: 2), "incarnation" => Feedback(2, incarnation: Guid.NewGuid()),
            "ship" => Feedback(2, ship: Guid.NewGuid()), "duplicate" => Feedback(),
            "expired" => Feedback(2, deadline: DateTime.UtcNow.AddSeconds(-1).Ticks),
            "future" => Feedback(2, deadline: DateTime.UtcNow.AddSeconds(5).Ticks),
            "missing" => new NetworkNavalLabFrames(manifest.IncarnationId, 1, 2, new float[24]), _ => Feedback(2)
        };
        fixture.ApplySailFeedback(value); TickView(); Assert.Equal("Invalid", vm.SailState); Assert.Equal(-1, vm.SailType);
    }

    [Fact]
    public void ExpiryAndAuthorityLossInvalidateWithoutNewMessages_AndValidFeedbackRestoresCache()
    {
        InstallPresentation(); fixture.ApplySailFeedback(Feedback()); TickView();
        fixture.sailFeedbackDeadline = 0; TickView(); Assert.Equal(-1, vm.SailType);
        fixture.ApplySailFeedback(Feedback(2)); TickView(); Assert.Equal("Full", vm.SailState);
        fixture.NativeAuthority = () => false; TickView(); Assert.Equal("Invalid", vm.SailState);
    }

    [Fact]
    public void ForeignViewedShipCannotDisplayLocalOwnerFeedback()
    {
        InstallPresentation(); fixture.ApplySailFeedback(Feedback()); TickView(); Assert.Equal("Full", vm.SailState);
        Set(view, "_playerControlledShip", fixture.Ships[0]); TickView();
        Assert.Equal("Invalid", vm.SailState); Assert.Equal(-1, vm.SailType);
    }

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

    [Fact]
    public void HostAndOtherModesRetainNativeReadout()
    {
        InstallPresentation(); fixture.factoryHost = true; TickView(); Assert.Equal("Raised", vm.SailState);
        foreach (var mode in new[] { NavalLabMode.Activation, NavalLabMode.HeldHelm, NavalLabMode.SingleClientNative, NavalLabMode.FactoryAuthorityProbe })
        {
            int count = mode == NavalLabMode.SingleClientNative ? 1 : 2;
            var prior = new NavalLabManifest(manifest.InstanceId, manifest.IncarnationId, manifest.Controllers.Take(count).ToArray(),
                manifest.Combatants.Take(count * 5).ToArray(), manifest.Ships.Take(count).ToArray(), mode);
            var active = new NavalLabBehavior(prior, "A", null!, null!); Bind(active); NavalLabPhysicsPatches.Active = active;
            TickView(); Assert.Equal("Raised", vm.SailState); Assert.Null(active.ReadSailStates());
        }
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
        Assert.Equal(1, input.Sequence); Assert.Null(fixture.sailFeedback);
        Assert.Equal(0, sails[fixture.LocalShip][0].TargetSailSetting);
        Assert.StartsWith("rejected:", fixture.RequestSail(3)); Assert.Single(sent);
    }

    private static UIContext Context()
    {
        WidgetInfo.Refresh();
        var platform = new Mock<ITwoDimensionPlatform>();
        platform.SetupGet(p => p.ReferenceWidth).Returns(1920); platform.SetupGet(p => p.ReferenceHeight).Returns(1080);
        var context = new UIContext(new TwoDimensionContext(platform.Object, null, null), new Mock<IInputContext>().Object);
        var brushes = Shell<BrushFactory>();
        Set(brushes, "_brushes", new Dictionary<string, Brush>
        { ["DefaultBrush"] = new Brush(), ["Naval.Mission.ShipControl.Key.Description"] = new Brush() });
        AccessTools.PropertySetter(typeof(UIContext), "BrushFactory").Invoke(context, new object[] { brushes });
        var fonts = new FontFactory(new ResourceDepot());
        var language = (Language)Activator.CreateInstance(typeof(Language), true)!;
        Set(language, "<DefaultFont>k__BackingField", new Font("test"));
        Set(language, "<LanguageID>k__BackingField", "English");
        Set(fonts, "_currentLangugage", language);
        AccessTools.PropertySetter(typeof(UIContext), "FontFactory").Invoke(context, new object[] { fonts });
        AccessTools.PropertySetter(typeof(UIContext), "EventManager").Invoke(context, new object[] { Activator.CreateInstance(typeof(EventManager), BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, new object[] { context }, null)! });
        return context;
    }
    private void AttachLayer(UIContext context)
    {
        var layer = Shell<GauntletLayer>();
        AccessTools.PropertySetter(typeof(GauntletLayer), "UIContext").Invoke(layer, new object[] { context });
        Set(view, "_gauntletLayer", layer);
    }

    [Fact]
    public void UnavailableLabelIsNoninteractiveUniqueAndRetiredOnNativeViewFinalizationAndReinitialization()
    {
        navigationCreated = GauntletGamepadNavigationManager.Instance == null;
        if (navigationCreated) GauntletGamepadNavigationManager.Initialize();
        var context = Context(); AttachLayer(context); InstallPresentation(); TickView();
        var label = Assert.IsType<TextWidget>(Assert.Single(context.Root.Children));
        Assert.Equal("Sail status unavailable", label.Text); Assert.True(label.IsVisible);
        Assert.True(label.DoNotAcceptEvents); Assert.False(label.IsFocusable);
        TickView(); Assert.Same(label, Assert.Single(context.Root.Children));
        fixture.ApplySailFeedback(Feedback()); TickView(); Assert.False(label.IsVisible);
        fixture.sailFeedbackDeadline = 0; TickView(); Assert.True(label.IsVisible);
        for (int i = 0; i < 3; i++)
        {
            var icon = new TaleWorlds.GauntletUI.ExtraWidgets.ValueBasedVisibilityWidget(context)
            { IndexToBeVisible = i, IndexToWatch = vm.SailType };
            Assert.False(icon.IsVisible);
            icon.IndexToWatch = i; Assert.True(icon.IsVisible);
        }
        var replacement = Context(); AttachLayer(replacement); TickView();
        Assert.Empty(context.Root.Children); Assert.Null(label.ParentWidget);
        Assert.Single(replacement.Root.Children);
        var finalizer = typeof(NavalLabNativePatches).GetNestedType("SailPresentationFinalization", BindingFlags.NonPublic)!;
        AccessTools.Method(finalizer, "Prefix").Invoke(null, new object[] { view });
        Assert.Empty(replacement.Root.Children); Assert.Null(fixture.sailPresentationView); Assert.Null(fixture.sailUnavailableLabel);
        AccessTools.Method(finalizer, "Prefix").Invoke(null, new object[] { view });
        AttachLayer(Context()); TickView(); Assert.NotNull(fixture.sailUnavailableLabel);
        Assert.Equal("Invalid", vm.SailState);
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

    public void Dispose() { fixture?.DetachSailPresentation(view); if (navigationCreated) GauntletGamepadNavigationManager.Instance.OnFinalize(); harmony.UnpatchAll(harmony.Id); NavalLabPhysicsPatches.Active = null; sails.Clear(); scope.Dispose(); }
}
#endif
