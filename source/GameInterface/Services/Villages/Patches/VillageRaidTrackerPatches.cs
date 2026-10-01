using Common;
using HarmonyLib;
using SandBox.GauntletUI.Map;
using SandBox.View.Map;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.MountAndBlade;

namespace GameInterface.Services.Villages.Patches;

[HarmonyPatch(typeof(Village), nameof(Village.VillageState), MethodType.Setter)]
internal class VillageRaidTrackerPatches
{
    [HarmonyPostfix]
    internal static void Postfix(Village __instance)
    {
        if (ModInformation.IsServer) return;

        var nameplate = MapScreen.Instance?
            .GetMapView<GauntletMapSettlementNameplateView>()?
            ._dataSource?
            .GetNameplateOfSettlement(__instance.Settlement);
        if (nameplate == null) return;

        // Synced village state does not run the map-event callbacks that update vanilla bookmarks.
        if (__instance.VillageState == Village.VillageStates.BeingRaided)
        {
            var faction = __instance.MapFaction;
            var hero = Hero.MainHero;
            if (hero != null && ShouldAutoTrackRaid(BannerlordConfig.AutoTrackAttackedSettlements,
                faction == hero.MapFaction, faction.Leader == hero))
            {
                nameplate.Track();
            }
        }
        else
        {
            nameplate.OnMapEventEndedOnSettlement();
        }
    }

    internal static bool ShouldAutoTrackRaid(int autoTrack, bool sameFaction, bool isFactionLeader) =>
        sameFaction && (autoTrack == 0 || (autoTrack == 1 && isFactionLeader));
}
