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
using static Coop.Tests.Missions.Battles.NavalLabTestShells;

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
        var id = Guid.NewGuid();
        var manifest = new NavalLabManifest("naval-lab:" + id.ToString("N"), id, new[] { "A", "B" },
            Enumerable.Range(0, 10).Select(_ => Guid.NewGuid()).ToArray(), new[] { Guid.NewGuid(), Guid.NewGuid() }, mode);
        return new NavalLabBehavior(manifest, "A", null!, null!);
    }


    [Theory]
    [InlineData(NavalLabMode.TwoClientNative, 24f)]
    [InlineData(NavalLabMode.TwoClientNativeAllPhysics, 60f)]
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
