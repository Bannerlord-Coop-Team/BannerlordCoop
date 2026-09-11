#if DEBUG
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using Common;
using HarmonyLib;
using Missions.Battles;
using Missions.Messages;
using Missions.Naval;
using Moq;
using NavalDLC.GauntletUI.MissionViews;
using NavalDLC.Missions.Objects;
using NavalDLC.Missions.Objects.UsableMachines;
using NavalDLC.Missions.ShipControl;
using NavalDLC.Missions.ShipInput;
using NavalDLC.View.MissionViews;
using Newtonsoft.Json;
using TaleWorlds.Engine;
using TaleWorlds.InputSystem;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
using TaleWorlds.MountAndBlade.View.MissionViews;
using TaleWorlds.MountAndBlade.View.Screens;
using TaleWorlds.ScreenSystem;
using Xunit;

namespace Coop.Tests.Missions.Battles;

[Collection("Mission.Current")]
public sealed class NavalLabControlPulseTests : IDisposable
{
    private readonly Harmony harmony = new("coop.tests.naval.control-pulse");
    private readonly MissionCurrentScope scope = new();
    private readonly NavalLabBehavior fixture;
    private readonly MissionGauntletShipControlView view;
    private readonly List<NetworkNavalLabHelmInput> sent = new();
    private static bool permission;
    private static bool gameThread;
    private static IInputContext input = null!;
    public NavalLabControlPulseTests()
    {
        permission = gameThread = true;
        Patch(AccessTools.Method(typeof(SoundEvent), nameof(SoundEvent.GetEventIdFromString)), nameof(Zero));
        Patch(AccessTools.Method(typeof(MBAnimation), nameof(MBAnimation.GetActionCodeWithName)), nameof(Zero));
        Patch(AccessTools.PropertyGetter(typeof(GameThread), nameof(GameThread.IsGameThread)), nameof(GameThreadValue));
        Patch(AccessTools.Method(typeof(NavalLabBehavior), "HasNativeInputPermission"), nameof(Permission));
        Patch(AccessTools.PropertyGetter(typeof(MissionView), "Input"), nameof(Input));
        Patch(AccessTools.PropertyGetter(typeof(Time), nameof(Time.ApplicationTime)), nameof(TimeValue));
        Patch(AccessTools.Method(typeof(PlayerShipController), nameof(PlayerShipController.SetInput)), nameof(ForbiddenWrite));
        var id = Guid.NewGuid();
        fixture = new NavalLabBehavior(new NavalLabManifest("naval-lab:" + id.ToString("N"), id, new[] { "A", "B" },
            Enumerable.Range(0, 10).Select(_ => Guid.NewGuid()).ToArray(), new[] { Guid.NewGuid(), Guid.NewGuid() }, NavalLabMode.TwoClientNative), "B", null!, null!);
        view = Shell<MissionGauntletShipControlView>();
        foreach (MissionBehavior behavior in new MissionBehavior[] { fixture, view })
            AccessTools.PropertySetter(typeof(MissionBehavior), nameof(MissionBehavior.Mission)).Invoke(behavior, new object[] { scope.Instance });
        AccessTools.Field(typeof(Mission), "<MissionBehaviors>k__BackingField").SetValue(scope.Instance, new List<MissionBehavior> { fixture, view });
        AccessTools.Field(view.GetType(), "SailControl").SetValue(view, SailInput.Full);
        input = Mock.Of<IInputContext>();
        fixture.SendNativeInput = sent.Add;
    }
    private void Patch(MethodBase method, string prefix) => harmony.Patch(method, prefix: new HarmonyMethod(GetType(), prefix));
    private static bool Zero(ref int __result) { __result = 0; return false; }
    private static bool GameThreadValue(ref bool __result) { __result = gameThread; return false; }
    private static bool Permission(ref bool __result) { __result = permission; return false; }
    private static bool Input(ref IInputContext __result) { __result = input; return false; }
    private static bool TimeValue(ref float __result) { __result = 100; return false; }
    private static bool ForbiddenWrite() => throw new InvalidOperationException("owner pulse must not write a simulator or follower controller");
    private static T Shell<T>()
    {
        var managed = typeof(ScriptComponentBehavior).BaseType!.Assembly.GetType("TaleWorlds.DotNet.Managed")!;
        var field = AccessTools.Field(managed, "_moduleTypes"); var previous = field.GetValue(null);
        try { if (previous == null) field.SetValue(null, new Dictionary<string, Type>()); return (T)FormatterServices.GetUninitializedObject(typeof(T)); }
        finally { field.SetValue(null, previous); }
    }
    private string Request(Guid? id = null, float lateral = 0.5f, bool row = true, long? deadline = null, int slot = 1)
        => fixture.RequestAxesPulse(id ?? Guid.NewGuid(), slot, lateral, row, deadline ?? DateTime.UtcNow.AddSeconds(1).Ticks);

    [Theory]
    [InlineData(-1f, false)]
    [InlineData(0f, true)]
    [InlineData(0.5f, true)]
    [InlineData(0f, false)]
    public void RealNativeConvertersReceiveSyntheticAxesAndPreserveCompleteSail_NoControllerWrite(float lateral, bool row)
    {
        Assert.StartsWith("requested:synthetic", Request(lateral: lateral, row: row));
        var message = Assert.Single(sent);
        Assert.Equal(1, message.Ship); Assert.Equal(1, message.Epoch); Assert.True(message.IsValid); Assert.True(message.HasHelm);
        Assert.Equal(Math.Min(Math.Abs(lateral) * 1.4f, 1) * Math.Sign(lateral), message.Rudder);
        Assert.Equal(2, message.Sail);
        Assert.Equal(row ? 1 : 0, message.Longitudinal);
        Assert.Equal(1, message.Sequence);
        Assert.Equal(message.Sequence, fixture.pulseFirstInputSequence);
        Assert.Equal(message.Sequence, fixture.pulseLastInputSequence);
        Assert.True(message.DeadlineUtcTicks <= DateTime.UtcNow.AddSeconds(1).Ticks);
    }

    [Theory]
    [InlineData("native-axes-backward", -1)]
    [InlineData("native-axes-neutral", 0)]
    [InlineData("native-row-stop", 2)]
    public void PresentationPulsesKeepExistingOwnerRelayAndSail(string kind, int expectedLongitudinal)
    {
        Assert.StartsWith("requested:synthetic", fixture.RequestPresentationPulse(Guid.NewGuid(), 1, 0, kind, DateTime.UtcNow.AddSeconds(1).Ticks));
        var message = Assert.Single(sent);
        Assert.Equal(expectedLongitudinal, message.Longitudinal);
        Assert.Equal(2, message.Sail);
        Assert.True(message.HasHelm);
        Assert.Equal(1, message.Ship);
        Assert.True(message.DeadlineUtcTicks <= DateTime.UtcNow.AddSeconds(1).Ticks);
    }

    [Fact]
    public void DuplicateDoesNotRestartOrExtendAndConflictDoesNotReplacePendingPulse()
    {
        var operation = Guid.NewGuid(); Assert.StartsWith("requested:", Request(operation));
        var deadline = fixture.pulseDeadline; var utc = fixture.pulseDeadlineUtcTicks;
        Assert.StartsWith("requested:", Request(operation)); Assert.Single(sent);
        Assert.StartsWith("rejected:conflicting", Request(operation, lateral: -1));
        Assert.Equal("rejected:axes_pulse_pending", Request());
        Assert.Equal(deadline, fixture.pulseDeadline); Assert.Equal(utc, fixture.pulseDeadlineUtcTicks);
        for (int i = 0; i < 5; i++) { fixture.nextNativeInput = 0; fixture.TickAxesPulse(); }
        Assert.All(sent, message => Assert.Equal(utc, message.DeadlineUtcTicks));
        fixture.pulseDeadline = 0; fixture.TickAxesPulse();
        int count = sent.Count;
        Assert.StartsWith("requested:", Request(operation)); fixture.TickAxesPulse(); Assert.Equal(count, sent.Count);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void NormalExpiryUsesRealZeroAxisConversionAndCurrentSailThenEndsWithoutRefresh(int sail)
    {
        Request();
        AccessTools.Field(view.GetType(), "SailControl").SetValue(view, (SailInput)sail);
        fixture.pulseDeadline = 0;
        fixture.TickAxesPulse();
        var neutral = sent.Last();
        Assert.True(neutral.HasHelm); Assert.Equal(0, neutral.Rudder); Assert.Equal(0, neutral.Lateral);
        Assert.Equal(0, neutral.Longitudinal); Assert.Equal(0, neutral.DoubleTap); Assert.Equal(sail, neutral.Sail);
        Assert.Equal("completed_axes_neutral_requested", fixture.pulsePhase); Assert.False(fixture.pulsePending);
        Assert.Equal(neutral.Sequence, fixture.pulseNeutralInputSequence);
        Assert.True(fixture.pulseNeutralInputSequence > fixture.pulseLastInputSequence);
        int count = sent.Count; for (int i = 0; i < 10; i++) fixture.TickAxesPulse(); Assert.Equal(count, sent.Count);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void PermissionLossOrCancellationUsesExistingSafetyStopNotSailPreservingCompletion(bool cancel)
    {
        Request();
        if (cancel) fixture.CancelAxesPulse("cancelled_safety_stop");
        else { permission = false; fixture.TickAxesPulse(); }
        Assert.False(sent.Last().HasHelm); Assert.Equal(0, sent.Last().Sail); Assert.False(fixture.pulsePending);
        int count = sent.Count; fixture.TickAxesPulse(); Assert.Equal(count, sent.Count);
        Assert.Contains("safety_stop", fixture.pulsePhase);
    }

    [Theory]
    [InlineData("permission")]
    [InlineData("thread")]
    [InlineData("owner")]
    [InlineData("expired")]
    [InlineData("future")]
    [InlineData("nan")]
    [InlineData("infinity")]
    [InlineData("range")]
    public void InvalidRequestCannotPublishOrAcquirePulse(string condition)
    {
        permission = condition != "permission"; gameThread = condition != "thread";
        string result = Request(slot: condition == "owner" ? 0 : 1,
            lateral: condition == "nan" ? float.NaN : condition == "infinity" ? float.PositiveInfinity : condition == "range" ? 2 : 0,
            deadline: DateTime.UtcNow.AddSeconds(condition == "expired" ? -1 : condition == "future" ? 2 : 1).Ticks);
        Assert.StartsWith("rejected:", result); Assert.Empty(sent); Assert.False(fixture.pulsePending);
    }

    [Fact]
    public void StatusIsFiniteReadOnlyBoundedAndOffThreadDoesNotTouchNativeState()
    {
        Request(); fixture.factoryTerminal = true;
        string json = JsonConvert.SerializeObject(fixture.InspectControlStatus());
        Assert.DoesNotContain("NaN", json); Assert.DoesNotContain("Infinity", json); Assert.Contains("not_ready_or_terminal", json);
        Assert.Contains("local read", json); Assert.Single(sent);
        gameThread = false;
        Assert.Contains("not_game_thread", JsonConvert.SerializeObject(fixture.InspectControlStatus()));
        Assert.Single(sent);
    }
    private static Agent captain = null!;
    private static bool dialog;
    private static bool Main(ref Agent __result) { __result = captain; return false; }
    private static bool True(ref bool __result) { __result = true; return false; }
    private static bool Dialog(ref bool __result) { __result = dialog; return false; }
    private static void Set(object target, string name, object? value) => AccessTools.Field(target.GetType(), name).SetValue(target, value);

    [Theory]
    [InlineData("allowed")]
    [InlineData("window")]
    [InlineData("screen")]
    [InlineData("dialog")]
    [InlineData("view")]
    [InlineData("used_object")]
    [InlineData("user")]
    [InlineData("deployment")]
    [InlineData("terminal")]
    public void RealOwnerPermissionAndHelmSelectionGatesPulseWithoutFocusOrOccupancyWrites(string condition)
    {
        harmony.Unpatch(AccessTools.Method(typeof(NavalLabBehavior), "HasNativeInputPermission"), HarmonyPatchType.Prefix, harmony.Id);
        Patch(AccessTools.PropertyGetter(typeof(NavalLabBehavior), "CanUseNativeControls"), nameof(True));
        fixture.nativeAutoHelmObserved = true;
        Patch(AccessTools.PropertyGetter(typeof(NavalLabBehavior), "HelmReplicasReady"), nameof(True));
        Patch(AccessTools.PropertyGetter(typeof(Mission), nameof(Mission.MainAgent)), nameof(Main));
        Patch(AccessTools.PropertyGetter(typeof(Agent), nameof(Agent.IsPlayerControlled)), nameof(True));
        Patch(AccessTools.PropertyGetter(typeof(MissionShipControlView), "IsDisplayingADialog"), nameof(Dialog));
        fixture.Ships = new[] { Shell<MissionShip>(), Shell<MissionShip>() };
        fixture.Agents = Enumerable.Range(0, 10).Select(_ => Shell<Agent>()).ToArray();
        captain = fixture.LocalCaptain;
        var helm = Shell<ShipControllerMachine>(); var point = Shell<StandingPoint>();
        Set(helm, "<PilotStandingPoint>k__BackingField", point);
        Set(fixture.LocalShip, "<ShipControllerMachine>k__BackingField", helm);
        Set(captain, "<CurrentlyUsedGameObject>k__BackingField", condition == "used_object" ? null : point);
        Set(point, "_userAgent", condition == "user" ? null : captain);
        Set(view, "<ControllerMachine>k__BackingField", condition == "view" ? null : helm);
        var screen = Shell<MissionScreen>();
        Set(view, "<MissionScreen>k__BackingField", screen);
        bool previousFocus = ScreenManager._isWindowFocused; var previousScreen = ScreenManager.TopScreen;
        try
        {
            ScreenManager._isWindowFocused = condition != "window";
            AccessTools.PropertySetter(typeof(ScreenManager), nameof(ScreenManager.TopScreen)).Invoke(null,
                new object?[] { condition == "screen" ? null : screen });
            dialog = condition == "dialog";
            fixture.nativeDeploymentComplete = condition != "deployment"; fixture.factoryTerminal = condition == "terminal";
            string result = Request();
            Assert.Equal(condition == "allowed", result.StartsWith("requested:"));
            Assert.Equal(condition == "allowed" ? 1 : 0, sent.Count);
            Assert.Same(condition == "user" ? null : captain, point.UserAgent);
            Assert.Same(condition == "used_object" ? null : point, captain.CurrentlyUsedGameObject);
        }
        finally
        {
            ScreenManager._isWindowFocused = previousFocus;
            AccessTools.PropertySetter(typeof(ScreenManager), nameof(ScreenManager.TopScreen)).Invoke(null, new object?[] { previousScreen });
        }
    }

    [Fact]
    public void DispatchExceptionCannotBeRetriedByPulseTickAndMarksSafetyHold()
    {
        fixture.SendNativeInput = _ => throw new InvalidOperationException("substituted transport failure");
        var id = Guid.NewGuid();
        Assert.Equal("failed:synthetic_axes_dispatch_uncertain", Request(id));
        Assert.False(fixture.pulsePending); Assert.NotNull(fixture.Blocker);
        Assert.Equal("failed:synthetic_axes_dispatch_uncertain", Request(id));
        fixture.TickAxesPulse(); Assert.Empty(sent);
    }

    [Fact]
    public void RealHostSetterAndProcessorDistinguishNormalAxisNeutralFromUnchangedSafetyStop()
    {
        harmony.Unpatch(AccessTools.Method(typeof(PlayerShipController), nameof(PlayerShipController.SetInput)), HarmonyPatchType.Prefix, harmony.Id);
        Patch(AccessTools.PropertyGetter(typeof(NavalLabBehavior), "CanUseNativeControls"), nameof(True));
        fixture.Ships = new[] { Shell<MissionShip>(), Shell<MissionShip>() };
        var player = new PlayerShipController(fixture.LocalShip);
        Set(fixture.LocalShip, "<Controller>k__BackingField", player);
        Request();
        fixture.factoryHost = false; fixture.ApplyNativeInput(sent[0]);
        Assert.Equal(SailInput.Raised, player.Update(0).Sail);
        fixture.factoryHost = true; fixture.ApplyNativeInput(sent[0]);
        Assert.Equal(SailInput.Full, player.Update(0).Sail); Assert.NotEqual(0, player.Update(0).RudderLateral);
        fixture.pulseDeadline = 0; fixture.TickAxesPulse(); fixture.ApplyNativeInput(sent.Last());
        var neutral = player.Update(0);
        Assert.Equal(0, neutral.RudderLateral); Assert.Equal(RowerLongitudinalInput.None, neutral.RowerLongitudinal);
        Assert.Equal(SailInput.Full, neutral.Sail);
        var processor = new ShipInputProcessor(fixture.LocalShip);
        processor.OnParallelFixedTick(0.02f, in neutral);
        Assert.Equal(0f, AccessTools.Field(typeof(ShipInputProcessor), "_rowerThrust").GetValue(processor));
        Assert.Equal(0f, AccessTools.Field(typeof(ShipInputProcessor), "_rowerRotation").GetValue(processor));
        Assert.Equal(1f, AccessTools.Field(typeof(ShipInputProcessor), "_squareSailSetting").GetValue(processor));
        fixture.NeutralizeNativeInput(1);
        Assert.Equal(SailInput.Raised, player.Update(0).Sail);
        Assert.Equal(RowerLongitudinalInput.Stop, player.Update(0).RowerLongitudinal);
    }

    private static bool UnavailableShip(ref NavalLabShipSnapshot __result)
    {
        __result = new NavalLabShipSnapshot(Guid.Empty, "substituted_native_observation_boundary");
        return false;
    }

    [Theory]
    [InlineData(float.NaN)]
    [InlineData(float.PositiveInfinity)]
    public void NonfiniteCurrentControllerInputIsUnavailableNotNonfiniteJson(float rudder)
    {
        harmony.Unpatch(AccessTools.Method(typeof(PlayerShipController), nameof(PlayerShipController.SetInput)), HarmonyPatchType.Prefix, harmony.Id);
        Patch(AccessTools.PropertyGetter(typeof(NavalLabBehavior), "CanUseNativeControls"), nameof(True));
        Patch(AccessTools.Method(typeof(NavalLabBehavior), nameof(NavalLabBehavior.InspectShip)), nameof(UnavailableShip));
        fixture.Ships = new[] { Shell<MissionShip>(), Shell<MissionShip>() };
        var player = new PlayerShipController(fixture.LocalShip);
        Set(fixture.LocalShip, "<Controller>k__BackingField", player);
        var record = ShipInputRecord.Stop(); record.SetRudderLateral(rudder); player.SetInput(in record);
        fixture.factoryHost = true;
        var json = JsonConvert.SerializeObject(fixture.InspectControlStatus());
        Assert.DoesNotContain("NaN", json); Assert.DoesNotContain("Infinity", json);
        Assert.Contains("\"currentHostApplication\":[null,null]", json);
        Assert.Empty(sent);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void NeutralDispatchFailureCannotRearmPulseOrClaimCompletion(bool safety)
    {
        Request(); fixture.SendNativeInput = _ => throw new InvalidOperationException("substituted transport failure");
        if (safety)
        {
            fixture.CancelAxesPulse("cancelled_safety_stop");
            Assert.NotNull(fixture.Blocker);
        }
        else
        {
            fixture.pulseDeadline = 0;
            Assert.Throws<InvalidOperationException>(() => fixture.TickAxesPulse());
        }
        Assert.False(fixture.pulsePending); Assert.False(fixture.pulseCompleting);
        Assert.StartsWith("failed_", fixture.pulsePhase); Assert.Equal(0, fixture.pulseNeutralInputSequence);
        fixture.TickAxesPulse(); Assert.Single(sent);
    }

    public void Dispose() { harmony.UnpatchAll(harmony.Id); scope.Dispose(); }
}
#endif
