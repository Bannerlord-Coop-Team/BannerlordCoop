#if DEBUG
using HarmonyLib;
using Missions.Battles;
using Missions.Naval;
using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.MountAndBlade;
using Xunit;

namespace Coop.Tests.Missions.Battles;

[Collection("Mission.Current")]
public sealed class NavalLabSingleClientTests : IDisposable
{
    private readonly MissionCurrentScope scope = new();
    private readonly NavalLabBehavior behavior;

    public NavalLabSingleClientTests()
    {
        behavior = new NavalLabBehavior(Manifest(), "A", null!, null!);
        AccessTools.PropertySetter(typeof(MissionBehavior), nameof(MissionBehavior.Mission))
            .Invoke(behavior, new object[] { scope.Instance });
        AccessTools.Field(typeof(Mission), "_activeAgents").SetValue(scope.Instance,
            new TaleWorlds.MountAndBlade.Missions.AgentList(0));
        AccessTools.Field(typeof(Mission), "<Teams>k__BackingField")
            .SetValue(scope.Instance, new Mission.TeamCollection(scope.Instance));
        AccessTools.Field(typeof(Mission), "<MissionBehaviors>k__BackingField")
            .SetValue(scope.Instance, new List<MissionBehavior> { behavior });
    }

    private static NavalLabManifest Manifest()
    {
        var id = Guid.NewGuid();
        return new NavalLabManifest("naval-lab:" + id.ToString("N"), id, new[] { "A" },
            Enumerable.Range(0, 5).Select(_ => Guid.NewGuid()).ToArray(), new[] { Guid.NewGuid() }, NavalLabMode.SingleClientNative);
    }

    [Fact]
    public void Manifest_OneOwnerIsExplicitImmutableAndNotASecondPlayer()
    {
        var manifest = Manifest();
        var copy = manifest.Controllers;
        copy[0] = "B";
        Assert.Equal("A", Assert.Single(manifest.Controllers));
        Assert.Single(manifest.Ships);
        Assert.Equal(5, manifest.Combatants.Length);
        foreach (var mode in new[] { NavalLabMode.Activation, NavalLabMode.HeldHelm })
            Assert.Throws<ArgumentException>(() => new NavalLabManifest(manifest.InstanceId, manifest.IncarnationId,
                manifest.Controllers, manifest.Combatants, manifest.Ships, mode));
        Assert.Throws<ArgumentException>(() => new NavalLabManifest(manifest.InstanceId, manifest.IncarnationId,
            new[] { "A", "A" }, manifest.Combatants, manifest.Ships, NavalLabMode.SingleClientNative));
    }

    [Fact]
    public void NativeInputAndDeployment_RejectMissingAuthorityAndMissingRealActorIdentity()
    {
        Assert.False(behavior.CanUseNativeControls);
        Assert.Equal("rejected:owner_not_ready", behavior.CompleteNativeDeployment());
        behavior.NativeAuthority = () => true;
        Assert.False(behavior.CanUseNativeControls);
        Assert.Equal("rejected:owner_not_ready", behavior.CompleteNativeDeployment());
        Assert.False(behavior.nativeDeploymentAttempted);
        Assert.False(scope.Instance.IsDeploymentFinished);
    }

    [Fact]
    public void FactoryActiveMode_ReadinessDoesNotInvokeEnableOrRequireAHoldBeforeDeployment()
    {
        behavior.SetAuthority(false);
        behavior.SetAuthority(true);
        Assert.Empty(behavior.ActivationTransitions);
        Assert.False(behavior.nativeTerminalHold);
        Assert.False(behavior.Simulating);
        Assert.Null(behavior.Blocker);
    }

    [Fact]
    public void HoldBeforeDeployment_IsIrreversibleAndRetainsDisposableGuard()
    {
        behavior.Hold();
        behavior.SetAuthority(true);
        Assert.True(behavior.nativeTerminalHold);
        Assert.True(behavior.RequiresProcessExit);
        Assert.False(behavior.CanUseNativeControls);
        Assert.Equal("rejected:fixture_blocked", behavior.CompleteNativeDeployment());
    }

    [Fact]
    public void ActualMissionDispatch_ReachesBothLifecycleCallbacksAndSetsNativeFlag()
    {
        var observer = new DeploymentObserver();
        scope.Instance.MissionBehaviors.Add(observer);
        scope.Instance.OnDeploymentFinished();
        scope.Instance.OnAfterDeploymentFinished();
        Assert.True(scope.Instance.IsDeploymentFinished);
        Assert.Equal(1, behavior.nativeDeploymentCallbacks);
        Assert.Equal(1, behavior.nativeAfterDeploymentCallbacks);
        Assert.Equal(new[] { "deployment", "after" }, observer.Calls);
        // These callbacks alone cannot grant input or claim the adapter finished native setup.
        Assert.False(behavior.nativeDeploymentComplete);
        Assert.False(behavior.CanUseNativeControls);
    }

    [Fact]
    public void NativeInputGuard_IsModeAndMissionScopedWithoutAnAlwaysTrueOwnershipStub()
    {
        NavalLabPhysicsPatches.Active = behavior;
        Assert.False(NavalLabPhysicsPatches.NativeInputAllowed(scope.Instance));
        Assert.True(NavalLabPhysicsPatches.NativeInputAllowed(null!));
        behavior.NativeAuthority = () => true;
        behavior.nativeDeploymentComplete = true;
        Assert.False(NavalLabPhysicsPatches.NativeInputAllowed(scope.Instance));
        NavalLabPhysicsPatches.Active = null;
        Assert.True(NavalLabPhysicsPatches.NativeInputAllowed(scope.Instance));
    }

    [Fact]
    public void InstalledNativePatchTargets_AllBindIncludingDiscreteInputAndOrders()
    {
        var harmony = new Harmony("coop.tests.naval.single.bindings");
        try
        {
            harmony.PatchAll(typeof(NavalMissionAdapter).Assembly);
            Assert.NotEmpty(harmony.GetPatchedMethods());
        }
        finally { harmony.UnpatchAll(harmony.Id); }
    }

    private sealed class DeploymentObserver : MissionLogic
    {
        public List<string> Calls { get; } = new();
        public override void OnDeploymentFinished() => Calls.Add("deployment");
        public override void OnAfterDeploymentFinished() => Calls.Add("after");
    }

    public void Dispose()
    {
        NavalLabPhysicsPatches.Active = null;
        scope.Dispose();
    }
}
#endif
