#if DEBUG
using HarmonyLib;
using Missions.Naval;
using System;
using TaleWorlds.Engine;
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
        var engine = new NavalShipEngine(null!, null!);

        engine.ApplyStationUse(null!, ShipTestHulls.CreateWorldItem(), inUse: true);
        engine.ApplyStationUse(null!, ShipTestHulls.CreatePoint(), inUse: true);
    }

    // A machine's deactivation is recomputed from local state and can lag its owner's, so the owner's use still seats it.
    [Fact]
    public void ApplyStationUse_StillSeatsOnADeactivatedMachine()
    {
        var engine = new NavalShipEngine(null!, null!);
        var point = ShipTestHulls.CreatePoint();
        AccessTools.Field(typeof(UsableMissionObject), "_isDeactivated").SetValue(point, true);
        AccessTools.Field(typeof(ScriptComponentBehavior), "_gameEntity").SetValue(point, ValidEntity());

        Assert.Throws<NullReferenceException>(() => engine.ApplyStationUse(null!, point, inUse: true));
    }

    // WeakGameEntity.IsValid only compares the pointer with zero; nothing here dereferences it.
    private static WeakGameEntity ValidEntity() =>
        (WeakGameEntity)AccessTools.Constructor(typeof(WeakGameEntity), new[] { typeof(UIntPtr) }).Invoke(new object[] { new UIntPtr(1) });
}
#endif
