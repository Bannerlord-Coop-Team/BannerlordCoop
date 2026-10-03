#if DEBUG
using System;
using System.Linq;
using System.Runtime.Serialization;
using Common;
using HarmonyLib;
using Missions.Battles;
using Missions.Messages;
using Missions.Naval;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
using TaleWorlds.MountAndBlade.View.MissionViews;
using Xunit;

namespace Coop.Tests.Missions.Battles;

[Collection("Mission.Current")]
public sealed class NavalLabDeckLocomotionTests : IDisposable
{
    private readonly Harmony harmony = new("coop.tests.naval.deck-locomotion");
    private readonly MissionCurrentScope scope = new();
    private static bool controlsReady;

    public NavalLabDeckLocomotionTests()
    {
        controlsReady = false;
        harmony.Patch(AccessTools.PropertyGetter(typeof(NavalLabBehavior), "CanUseNativeControls"),
            prefix: new HarmonyMethod(typeof(NavalLabDeckLocomotionTests), nameof(ControlsReady)));
        harmony.Patch(AccessTools.PropertyGetter(typeof(GameThread), nameof(GameThread.IsGameThread)),
            prefix: new HarmonyMethod(typeof(NavalLabDeckLocomotionTests), nameof(True)));
    }

    private static bool ControlsReady(ref bool __result) { __result = controlsReady; return false; }
    private static bool True(ref bool __result) { __result = true; return false; }

    private NavalLabBehavior Behavior(NavalLabMode mode = NavalLabMode.TwoClientNative)
    {
        var id = Guid.NewGuid();
        var manifest = new NavalLabManifest("naval-lab:" + id.ToString("N"), id, new[] { "A", "B" },
            Enumerable.Range(0, 10).Select(_ => Guid.NewGuid()).ToArray(), new[] { Guid.NewGuid(), Guid.NewGuid() }, mode);
        var behavior = new NavalLabBehavior(manifest, "A", null!, null!);
        AccessTools.PropertySetter(typeof(MissionBehavior), nameof(MissionBehavior.Mission)).Invoke(behavior, new object[] { scope.Instance });
        return behavior;
    }

    private static NetworkNavalLabHelmOccupancy Offer(NavalLabBehavior behavior, bool occupied) =>
        new(behavior.manifest.IncarnationId, 1, 0, behavior.manifest.Ships[0], behavior.manifest.Combatants[0], "helm", 2, occupied, "offer");

    [Theory]
    [InlineData("jump")]
    [InlineData("crew")]
    public void OtherAgentControls_RemainKeyboardOnly(string kind)
    {
        Assert.Equal("rejected:keyboard_controls_only", Behavior().StartAgentControl(kind, 0, 0));
    }

    [Theory]
    [InlineData("all_physics", "rejected:wrong_mode")]
    [InlineData("not_ready", "rejected:fixture_not_ready_or_terminal")]
    [InlineData("helm_pending", "rejected:native_helm_transition")]
    [InlineData("helm_failed", "rejected:native_helm_transition")]
    [InlineData("offer_occupied", "rejected:helm_release_not_confirmed")]
    [InlineData("release_unconfirmed", "rejected:helm_release_not_confirmed")]
    public void SyntheticWalk_RequiresConfirmedStableRelease(string condition, string expected)
    {
        var behavior = Behavior(condition == "all_physics" ? NavalLabMode.TwoClientNativeAllPhysics : NavalLabMode.TwoClientNative);
        controlsReady = condition != "not_ready";
        behavior.nativeAutoHelmObserved = true;
        behavior.nativeHelmPhase = condition == "helm_pending" ? "pending" : condition == "helm_failed" ? "failed" : "observed_released";
        behavior.offeredHelm = Offer(behavior, condition == "offer_occupied");

        Assert.Equal(expected, behavior.StartAgentControl("walk", 0, 1));
        Assert.Null(behavior.controlledAgent);
    }

    [Fact]
    public void NativeHelmRequest_IsRejectedWhileSyntheticControlIsActive()
    {
        var behavior = Behavior();
        behavior.controlledAgent = (Agent)FormatterServices.GetUninitializedObject(typeof(Agent));

        Assert.Equal("rejected:agent_control_active", behavior.RequestNativeHelm(Guid.NewGuid(), 0, true));
        Assert.Equal(Guid.Empty, behavior.nativeHelmOperation);
    }

    [Fact]
    public void ActiveTurn_IsCancelledWhenReleaseStopsHolding_AndRestoresCustomLook()
    {
        var behavior = Behavior();
        var controller = (MissionMainAgentController)FormatterServices.GetUninitializedObject(typeof(MissionMainAgentController));
        controller.CustomLookDir = new Vec3(1f, 0f, 0f);
        behavior.deckTurnController = controller;
        behavior.deckTurnPreviousLook = Vec3.Zero;
        behavior.controlledAgent = (Agent)FormatterServices.GetUninitializedObject(typeof(Agent));
        behavior.controlKind = "turn";
        behavior.controlDeadline = double.MaxValue;

        behavior.TickAgentControl(0.01f);

        Assert.Null(behavior.controlledAgent);
        Assert.Null(behavior.deckTurnController);
        Assert.Equal(Vec3.Zero, controller.CustomLookDir);
    }

    public void Dispose()
    {
        harmony.UnpatchAll(harmony.Id);
        scope.Dispose();
    }
}
#endif
