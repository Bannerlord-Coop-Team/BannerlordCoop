#if COOP_NAVAL
using GameInterface.Policies;
using HarmonyLib;
using Missions.Naval;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using TaleWorlds.CampaignSystem.Naval;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;
using TaleWorlds.Library;
using Xunit;

namespace Coop.Tests.Missions.Battles;

// The menu refresh gate reads Mission.Current, which other test classes in this collection set.
[Collection("Mission.Current")]
public class CoopShipSnapshotPatchesTests
{
    // M6 D7: a snapshot's hits grant no XP here, but their mitigated damage is kept for the fate report; a campaign ship's pass.
    [Fact]
    public void DamageXp_SumsASnapshotsMitigatedDamage_AndLetsACampaignShipThrough()
    {
        var snapshot = new CoopShipSnapshotBuilder().Build(CreateOwnedShip());
        var ship = CreateOwnedShip();

        Assert.False(SnapshotShipDamageXpPatch.Prefix(snapshot, rawDamage: 100f, finalDamage: 70f));
        Assert.False(SnapshotShipDamageXpPatch.Prefix(snapshot, rawDamage: 40f, finalDamage: 50f));
        Assert.False(SnapshotShipDamageXpPatch.Prefix(snapshot, rawDamage: 20f, finalDamage: 15f));
        Assert.True(SnapshotShipDamageXpPatch.Prefix(ship, rawDamage: 100f, finalDamage: 70f));

        Assert.Equal(35f, CoopShipSnapshots.MitigatedDamageOf(snapshot));
        Assert.Equal(0f, CoopShipSnapshots.MitigatedDamageOf(ship));
    }

    // M6 D5: the server's applied ship fates reach clients still in the battle, where the menu context can have no handler.
    [Fact]
    public void MenuRefresh_IsHeldWhileAMissionIsCurrent()
    {
        using (new MissionCurrentScope())
            Assert.False(SnapshotShipMenuRefreshPatch.Prefix());

        Assert.True(SnapshotShipMenuRefreshPatch.Prefix());
    }

    [Fact]
    public void SnapshotHitPointWrites_RunTheDefaultPrioritySyncPrefixInsideTheScope()
    {
        var snapshot = new CoopShipSnapshotBuilder().Build(CreateOwnedShip());

        var recorded = RecordSyncPrefixScope(() =>
        {
            snapshot.HitPoints = 100f;
            snapshot.SailHitPoints = 50f;
        });

        Assert.Equal(new[] { true, true }, recorded);
        Assert.Equal(100f, snapshot.HitPoints);
        Assert.Equal(50f, snapshot.SailHitPoints);
        Assert.False(CallOriginalPolicy.AreOriginalsAllowedForCurrentOperation);
    }

    [Fact]
    public void CampaignShipHitPointWrites_RunTheSyncPrefixOutsideTheScope()
    {
        var ship = CreateOwnedShip();

        var recorded = RecordSyncPrefixScope(() =>
        {
            ship.HitPoints = 100f;
            ship.SailHitPoints = 50f;
        });

        Assert.Equal(new[] { false, false }, recorded);
        Assert.Equal(100f, ship.HitPoints);
    }

    [Fact]
    public void SnapshotHitPointWriteThatThrows_StillClosesTheScope()
    {
        var snapshot = new CoopShipSnapshotBuilder().Build(CreateOwnedShip());

        RecordSyncPrefixScope(() =>
        {
            throwFromSyncPrefix = true;
            Assert.Throws<InvalidOperationException>(() => snapshot.HitPoints = 100f);
        });

        Assert.False(CallOriginalPolicy.AreOriginalsAllowedForCurrentOperation);
    }

    [Fact]
    public void SnapshotHitPointWrites_HoldTheMenuRefreshOnlyWhileTheirPostfixesRun()
    {
        var snapshot = new CoopShipSnapshotBuilder().Build(CreateOwnedShip());
        var ship = CreateOwnedShip();

        var recorded = RecordPostfixMenuRefresh(() =>
        {
            snapshot.HitPoints = 100f;
            snapshot.SailHitPoints = 50f;
            ship.HitPoints = 100f;
        });

        Assert.Equal(new[] { false, false, true }, recorded);
        Assert.False(SnapshotShipHitPointsSyncScopePatch.IsWritingSnapshot);
        Assert.True(SnapshotShipMenuRefreshPatch.Prefix());
    }

    // The probe postfix stands in for the campaign HitPoints postfix that refreshes the open menu.
    private static List<bool> RecordPostfixMenuRefresh(Action writeHitPoints)
    {
        var probeHarmony = new Harmony("coop.tests.naval.ship_snapshot_menu_probe");
        var scopeHarmony = new Harmony("coop.tests.naval.ship_snapshot_menu_scope");
        try
        {
            var probe = new HarmonyMethod(typeof(CoopShipSnapshotPatchesTests), nameof(MenuRefreshProbe));
            probeHarmony.Patch(AccessTools.PropertySetter(typeof(Ship), nameof(Ship.HitPoints)), postfix: probe);
            probeHarmony.Patch(AccessTools.PropertySetter(typeof(Ship), nameof(Ship.SailHitPoints)), postfix: probe);
            scopeHarmony.CreateClassProcessor(typeof(SnapshotShipHitPointsSyncScopePatch)).Patch();
            recordedMenuRefreshes = new List<bool>();

            writeHitPoints();
            return recordedMenuRefreshes;
        }
        finally
        {
            recordedMenuRefreshes = null;
            scopeHarmony.UnpatchAll(scopeHarmony.Id);
            probeHarmony.UnpatchAll(probeHarmony.Id);
        }
    }

    [ThreadStatic]
    private static List<bool> recordedMenuRefreshes;

    private static void MenuRefreshProbe() => recordedMenuRefreshes?.Add(SnapshotShipMenuRefreshPatch.Prefix());

    // The probe stands in for the generated AutoSync setter prefix, which declares no priority. It is patched before
    // the scope patch, so only the priority can put the scope first.
    private static List<bool> RecordSyncPrefixScope(Action writeHitPoints)
    {
        var probeHarmony = new Harmony("coop.tests.naval.ship_snapshot_sync_probe");
        var scopeHarmony = new Harmony("coop.tests.naval.ship_snapshot_sync_scope");
        try
        {
            var probe = new HarmonyMethod(typeof(CoopShipSnapshotPatchesTests), nameof(SyncPrefixProbe));
            probeHarmony.Patch(AccessTools.PropertySetter(typeof(Ship), nameof(Ship.HitPoints)), prefix: probe);
            probeHarmony.Patch(AccessTools.PropertySetter(typeof(Ship), nameof(Ship.SailHitPoints)), prefix: probe);
            scopeHarmony.CreateClassProcessor(typeof(SnapshotShipHitPointsSyncScopePatch)).Patch();
            recordedScopes = new List<bool>();

            writeHitPoints();
            return recordedScopes;
        }
        finally
        {
            recordedScopes = null;
            throwFromSyncPrefix = false;
            scopeHarmony.UnpatchAll(scopeHarmony.Id);
            probeHarmony.UnpatchAll(probeHarmony.Id);
        }
    }

    [ThreadStatic]
    private static List<bool> recordedScopes;

    [ThreadStatic]
    private static bool throwFromSyncPrefix;

    private static void SyncPrefixProbe()
    {
        if (recordedScopes == null) return;

        recordedScopes.Add(CallOriginalPolicy.AreOriginalsAllowedForCurrentOperation);
        if (throwFromSyncPrefix) throw new InvalidOperationException("sync prefix failed");
    }

    private static Ship CreateOwnedShip()
    {
        var party = (PartyBase)RuntimeHelpers.GetUninitializedObject(typeof(PartyBase));
        party._ships = new MBList<Ship>();

        var hull = (ShipHull)RuntimeHelpers.GetUninitializedObject(typeof(ShipHull));
        hull.AvailableSlots = new MBReadOnlyDictionary<string, ShipSlot>(new Dictionary<string, ShipSlot>());
        typeof(ShipHull).GetProperty(nameof(ShipHull.MaxHitPoints))!.SetValue(hull, 1000);
        typeof(ShipHull).GetProperty(nameof(ShipHull.MaxSailHitPoints))!.SetValue(hull, 500);

        var ship = new Ship(hull) { Owner = party };
        ship.HitPoints = 600f;
        return ship;
    }
}
#endif
