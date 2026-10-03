#if DEBUG
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using HarmonyLib;
using Missions.Battles;
using Missions.Naval;
using NavalDLC.Missions.Objects;
using NavalDLC.Missions.Objects.UsableMachines;
using TaleWorlds.Engine;
using TaleWorlds.MountAndBlade;
using Xunit;
using Attachment = NavalDLC.Missions.Objects.UsableMachines.ShipAttachmentMachine.ShipAttachment;
using RopeState = NavalDLC.Missions.Objects.UsableMachines.ShipAttachmentMachine.ShipAttachment.ShipAttachmentState;

namespace Coop.Tests.Missions.Battles;

[Collection("Mission.Current")]
public sealed class NavalLabDeckBoardingTests : IDisposable
{
    private readonly Harmony harmony = new("coop.tests.naval.deck-boarding");
    private readonly MissionCurrentScope scope = new();
    private static int face;

    public NavalLabDeckBoardingTests()
    {
        harmony.Patch(AccessTools.Method(typeof(Agent), nameof(Agent.GetCurrentNavigationFaceId)),
            prefix: new HarmonyMethod(typeof(NavalLabDeckBoardingTests), nameof(Face)));
        harmony.Patch(AccessTools.Method(typeof(Agent), nameof(Agent.GetSteppedEntity)),
            prefix: new HarmonyMethod(typeof(NavalLabDeckBoardingTests), nameof(NoSteppedEntity)));
    }

    private static bool Face(ref int __result) { __result = face; return false; }
    private static bool NoSteppedEntity(ref WeakGameEntity __result) { __result = default; return false; }

    private static NavalLabBehavior Behavior(NavalLabMode mode)
    {
        int participants = mode == NavalLabMode.SingleClientNative ? 1 : 2;
        var id = Guid.NewGuid();
        var manifest = new NavalLabManifest("naval-lab:" + id.ToString("N"), id, new[] { "A", "B" }.Take(participants).ToArray(),
            Enumerable.Range(0, participants * NavalLabManifest.CrewPerShip).Select(_ => Guid.NewGuid()).ToArray(),
            Enumerable.Range(0, participants).Select(_ => Guid.NewGuid()).ToArray(), mode);
        return new NavalLabBehavior(manifest, "A", null!, null!);
    }

    private static void Set(object target, string name, object? value) => AccessTools.Field(target.GetType(), name).SetValue(target, value);
    private static T Shell<T>()
    {
        var managed = typeof(ScriptComponentBehavior).BaseType!.Assembly.GetType("TaleWorlds.DotNet.Managed")!;
        var field = AccessTools.Field(managed, "_moduleTypes"); var previous = field.GetValue(null);
        try { if (previous == null) field.SetValue(null, new Dictionary<string, Type>()); return (T)FormatterServices.GetUninitializedObject(typeof(T)); }
        finally { field.SetValue(null, previous); }
    }

    // Same engine-boundary shell as NavalLabSingleClientTeamAITests: NativeObject's initializer needs IManaged.
    private static GameEntity EntityAtEngineBoundary()
    {
        var field = AccessTools.Field(typeof(TaleWorlds.DotNet.NativeObject).Assembly
            .GetType("TaleWorlds.DotNet.LibraryApplicationInterface"), "IManaged");
        var previous = field.GetValue(null);
        try
        {
            field.SetValue(null, typeof(DispatchProxy).GetMethod(nameof(DispatchProxy.Create))!
                .MakeGenericMethod(field.FieldType, typeof(NativeReferenceBoundary)).Invoke(null, null));
            var entity = Shell<GameEntity>();
            GC.SuppressFinalize(entity);
            // GameEntity equality treats a zero pointer as null.
            Set(entity, "<Pointer>k__BackingField", new UIntPtr(0x1234));
            return entity;
        }
        finally { field.SetValue(null, previous); }
    }

    public class NativeReferenceBoundary : DispatchProxy
    {
        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args) =>
            targetMethod!.ReturnType == typeof(int) ? 0 : null;
    }

    [Theory]
    [InlineData(NavalLabMode.TwoClientNative, 24f)]
    [InlineData(NavalLabMode.TwoClientNativeAllPhysics, 60f)]
    [InlineData(NavalLabMode.Activation, 60f)]
    [InlineData(NavalLabMode.HeldHelm, 60f)]
    [InlineData(NavalLabMode.SingleClientNative, 60f)]
    [InlineData(NavalLabMode.FactoryAuthorityProbe, 60f)]
    public void OnlyTheRopeFixtureStartsInsideNativeHookRange(NavalLabMode mode, float spacing)
    {
        Assert.Equal(spacing, Behavior(mode).HullSpacing);
    }

    [Theory]
    [InlineData(RopeState.BridgeConnected, 102, 1)]
    [InlineData(RopeState.BridgeConnected, 105, -1)]
    [InlineData(RopeState.BridgeConnected, 99, -1)]
    [InlineData(RopeState.BridgeThrown, 102, -1)]
    [InlineData(RopeState.RopesPulling, 102, -1)]
    public void PlankSupport_RequiresConnectedPlankMembership_NotNearbyFaces(RopeState state, int captainFace, int expectedSlot)
    {
        var behavior = Behavior(NavalLabMode.TwoClientNative);
        var ships = new[] { Shell<MissionShip>(), Shell<MissionShip>() };
        var sources = new[] { Shell<ShipAttachmentMachine>(), Shell<ShipAttachmentMachine>() };
        var attachment = (Attachment)FormatterServices.GetUninitializedObject(typeof(Attachment));
        Set(attachment, "_state", state);
        Set(attachment, "_bridgeNavmeshId", 100);
        Set(attachment, "_navMeshBridge", EntityAtEngineBoundary());
        for (int slot = 0; slot < 2; slot++) Set(sources[slot], "<OwnerShip>k__BackingField", ships[slot]);
        Set(sources[1], "<CurrentAttachment>k__BackingField", attachment);
        behavior.Ships = ships;
        behavior.ropeSources = new[] { new[] { sources[0] }, new[] { sources[1] } };
        face = captainFace;

        Assert.Equal(expectedSlot, behavior.PlankSupportSlot((Agent)FormatterServices.GetUninitializedObject(typeof(Agent))));
    }

    [Fact]
    public void PlankSupport_IsUnavailableBeforeAnyRopeInventory()
    {
        face = 102;
        Assert.Equal(-1, Behavior(NavalLabMode.TwoClientNative).PlankSupportSlot((Agent)FormatterServices.GetUninitializedObject(typeof(Agent))));
    }

    public void Dispose()
    {
        harmony.UnpatchAll(harmony.Id);
        scope.Dispose();
    }
}
#endif
