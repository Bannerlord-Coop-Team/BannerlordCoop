using System.Runtime.CompilerServices;
using Common;
using HarmonyLib;
using SandBox.GauntletUI.Map;
using SandBox.View.Map;
using SandBox.ViewModelCollection.Nameplate;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.MountAndBlade;

namespace GameInterface.Services.Villages.Patches;

[HarmonyPatch]
internal class VillageRaidTrackerPatches
{
    // Keep manual tracking intent and apply off-map changes until the next nameplate replaces this one.
    private static readonly ConditionalWeakTable<Village, SettlementNameplateVM> nameplates = new();

    [HarmonyPatch(typeof(Village), nameof(Village.VillageState), MethodType.Setter)]
    [HarmonyPostfix]
    internal static void Postfix(Village __instance)
    {
        if (ModInformation.IsServer) return;

        var nameplate = MapScreen.Instance?
            .GetMapView<GauntletMapSettlementNameplateView>()?
            ._dataSource?
            .GetNameplateOfSettlement(__instance.Settlement);
        if (nameplate == null) nameplates.TryGetValue(__instance, out nameplate);
        if (nameplate == null) return;
        Refresh(__instance, nameplate);
    }

    [HarmonyPatch(typeof(SettlementNameplateVM), MethodType.Constructor)]
    [HarmonyPostfix]
    internal static void NameplateCreated(SettlementNameplateVM __instance)
    {
        if (ModInformation.IsServer || !__instance.Settlement.IsVillage) return;
        var village = __instance.Settlement.Village;
        var hadNameplate = nameplates.TryGetValue(village, out var previous);
        if (hadNameplate) __instance._isTrackedManually = previous._isTrackedManually;
        nameplates.Remove(village);
        nameplates.Add(village, __instance);
        if (hadNameplate || village.VillageState == Village.VillageStates.BeingRaided)
            Refresh(village, __instance);
    }

    private static void Refresh(Village village, SettlementNameplateVM nameplate)
    {
        // Synced village state does not run the map-event callbacks that update vanilla bookmarks.
        if (village.VillageState == Village.VillageStates.BeingRaided)
        {
            var faction = village.MapFaction;
            var hero = Hero.MainHero;
            if (hero != null && faction != null && ShouldAutoTrackRaid(BannerlordConfig.AutoTrackAttackedSettlements,
                faction == hero.MapFaction, faction.Leader == hero))
            {
                nameplate.Track();
            }
        }
        else if (!nameplate._isTrackedManually && BannerlordConfig.AutoTrackAttackedSettlements < 2 &&
            !village.Settlement.IsUnderSiege && !village.Settlement.IsUnderRaid &&
            !village.Settlement.InRebelliousState)
        {
            nameplate.Untrack();
        }
    }

    internal static bool ShouldAutoTrackRaid(int autoTrack, bool sameFaction, bool isFactionLeader) =>
        sameFaction && (autoTrack == 0 || (autoTrack == 1 && isFactionLeader));
}
