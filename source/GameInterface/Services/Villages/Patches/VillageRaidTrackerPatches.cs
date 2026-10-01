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
    private static readonly ConditionalWeakTable<Village, RaidBookmarkState> nameplates = new();

    [HarmonyPatch(typeof(Village), nameof(Village.VillageState), MethodType.Setter)]
    [HarmonyPostfix]
    internal static void Postfix(Village __instance)
    {
        if (ModInformation.IsServer) return;

        var nameplate = MapScreen.Instance?
            .GetMapView<GauntletMapSettlementNameplateView>()?
            ._dataSource?
            .GetNameplateOfSettlement(__instance.Settlement);
        var state = nameplates.GetValue(__instance, _ => new RaidBookmarkState());
        if (nameplate == null) nameplate = state.Nameplate;
        if (nameplate == null) return;
        state.Nameplate = nameplate;
        Refresh(__instance, nameplate, state);
    }

    [HarmonyPatch(typeof(SettlementNameplateVM), MethodType.Constructor)]
    [HarmonyPostfix]
    internal static void NameplateCreated(SettlementNameplateVM __instance)
    {
        if (ModInformation.IsServer || !__instance.Settlement.IsVillage) return;
        var village = __instance.Settlement.Village;
        var state = nameplates.GetValue(village, _ => new RaidBookmarkState());
        if (state.Nameplate != null) __instance._isTrackedManually = state.Nameplate._isTrackedManually;
        state.Nameplate = __instance;
        Refresh(village, __instance, state);
    }

    private static void Refresh(Village village, SettlementNameplateVM nameplate, RaidBookmarkState state)
    {
        // Synced village state does not run the map-event callbacks that update vanilla bookmarks.
        if (village.VillageState == Village.VillageStates.BeingRaided)
        {
            var faction = village.MapFaction;
            var hero = Hero.MainHero;
            if (hero == null || faction == null || !state.TryStartRaid()) return;
            if (ShouldAutoTrackRaid(BannerlordConfig.AutoTrackAttackedSettlements,
                faction == hero.MapFaction, faction.Leader == hero))
            {
                var wasTracked = Campaign.Current.VisualTrackerManager.CheckTracked(village.Settlement);
                nameplate.Track();
                if (!wasTracked) state.AutoTracked = true;
            }
        }
        else
        {
            // Village state ends the raid before its map-event teardown may arrive.
            var shouldRemove = ShouldRemoveRaidBookmark(state.AutoTracked, nameplate._isTrackedManually,
                BannerlordConfig.AutoTrackAttackedSettlements, village.Settlement.IsUnderSiege ||
                village.Settlement.InRebelliousState);
            state.EndRaid();
            if (shouldRemove) nameplate.Untrack();
        }
    }

    internal sealed class RaidBookmarkState
    {
        public SettlementNameplateVM Nameplate;
        public bool AutoTracked;
        private bool raidObserved;

        public bool TryStartRaid()
        {
            if (raidObserved) return false;
            raidObserved = true;
            return true;
        }

        public void EndRaid()
        {
            raidObserved = false;
            AutoTracked = false;
        }
    }

    internal static bool ShouldRemoveRaidBookmark(bool autoTracked, bool manuallyTracked, int autoTrack, bool activeAttack) =>
        autoTracked && !manuallyTracked && autoTrack < 2 && !activeAttack;

    internal static bool ShouldAutoTrackRaid(int autoTrack, bool sameFaction, bool isFactionLeader) =>
        sameFaction && (autoTrack == 0 || (autoTrack == 1 && isFactionLeader));
}
