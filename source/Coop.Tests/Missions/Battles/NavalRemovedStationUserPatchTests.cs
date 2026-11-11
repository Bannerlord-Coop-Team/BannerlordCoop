#if COOP_NAVAL
using HarmonyLib;
using Missions.Naval;
using NavalDLC.Missions.Objects.UsableMachines;
using System;
using Xunit;

namespace Coop.Tests.Missions.Battles;

/// <summary>Covers where the removed station user guard is applied; its release runs in E2E, where an agent can be built.</summary>
[Collection("Mission.Current")]
public class NavalRemovedStationUserPatchTests
{
    // Every hull machine tick the guard names resolves and gets the prefix; a throwing TargetMethods would stop all naval patching.
    [Fact]
    public void Patch_PrefixesEachHullMachineTick()
    {
        var harmony = new Harmony("coop.tests.naval.removed_station_user." + Guid.NewGuid());
        try
        {
            harmony.CreateClassProcessor(typeof(NavalRemovedStationUserPatch)).Patch();

            foreach (var machine in new[] { typeof(ShipControllerMachine), typeof(ShipPullingMachine), typeof(ShipAttachmentPointMachine) })
            {
                var tick = AccessTools.DeclaredMethod(machine, nameof(ShipControllerMachine.OnTick));
                Assert.Contains(Harmony.GetPatchInfo(tick).Prefixes, patch => patch.owner == harmony.Id);
            }
        }
        finally
        {
            harmony.UnpatchAll(harmony.Id);
        }
    }
}
#endif
