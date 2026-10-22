#if DEBUG
using HarmonyLib;
using Missions.Messages;
using Missions.Naval;
using System;
using TaleWorlds.Engine;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
using Xunit;

namespace Coop.Tests.Missions.Battles;

/// <summary>R41a-2: which points the naval engine seats a puppet on. A null agent shows whether the use was reached.</summary>
public class NavalShipEngineTests
{
    // W2: re-seating a puppet onto a picked-up item bound it to a freed object, a native AV.
    [Fact]
    public void ApplyStationUse_NeverSeatsOnAnItemOrARemovedPoint()
    {
        var engine = new NavalShipEngine(null!, null!, null!);

        engine.ApplyStationUse(null!, ShipTestHulls.CreateWorldItem(), inUse: true);
        engine.ApplyStationUse(null!, ShipTestHulls.CreatePoint(), inUse: true);
    }

    // A machine's deactivation is recomputed from local state and can lag its owner's, so the owner's use still seats it.
    [Fact]
    public void ApplyStationUse_StillSeatsOnADeactivatedMachine()
    {
        var engine = new NavalShipEngine(null!, null!, null!);
        var point = ShipTestHulls.CreatePoint();
        AccessTools.Field(typeof(UsableMissionObject), "_isDeactivated").SetValue(point, true);
        AccessTools.Field(typeof(ScriptComponentBehavior), "_gameEntity").SetValue(point, ValidEntity());

        Assert.Throws<NullReferenceException>(() => engine.ApplyStationUse(null!, point, inUse: true));
    }

    // R45 roll: the owner sends vanilla's crew centre in its body frame, so a copy at the same pose applies it at the same world point.
    [Fact]
    public void CrewLoadOf_IsVanillasCrewCentreInTheBodyFrame()
    {
        var rotation = Mat3.Identity;
        rotation.RotateAboutUp(1.2f);
        rotation.RotateAboutForward(0.07f);
        var body = new MatrixFrame(rotation, new Vec3(100f, 50f, 2f));
        var first = new Vec3(101f, 48f, 3f);
        var second = new Vec3(98f, 55f, 3.5f);

        var load = NavalShipEngine.CrewLoadOf(160f, (80f * first) + (80f * second), body, agentAboard: true);

        var centre = body.TransformToParent(load.LocalCentre);
        Assert.Equal(160f, load.Mass);
        Assert.Equal(99.5f, centre.x, 3);
        Assert.Equal(51.5f, centre.y, 3);
        Assert.Equal(3.25f, centre.z, 3);
    }

    // Nobody aboard (the crew boarded an enemy hull): a known-empty deck, so copies take no crew weight.
    [Fact]
    public void CrewLoadOf_IsEmptyWithNobodyAboard()
    {
        Assert.Same(BattleShipCrewLoad.Empty, NavalShipEngine.CrewLoadOf(0f, Vec3.Zero, MatrixFrame.Identity, agentAboard: false));
    }

    // Agents aboard but no mass committed is a frame vanilla skipped (dt 0), so it sends no load and copies keep the last.
    [Fact]
    public void CrewLoadOf_IsUnknownWithoutCommittedMassWhileAgentsStandAboard()
    {
        Assert.Null(NavalShipEngine.CrewLoadOf(0f, Vec3.Zero, MatrixFrame.Identity, agentAboard: true));
    }

    // WeakGameEntity.IsValid only compares the pointer with zero; nothing here dereferences it.
    internal static WeakGameEntity ValidEntity() =>
        (WeakGameEntity)AccessTools.Constructor(typeof(WeakGameEntity), new[] { typeof(UIntPtr) }).Invoke(new object[] { new UIntPtr(1) });
}
#endif
