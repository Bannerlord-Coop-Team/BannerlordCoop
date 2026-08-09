using Common;
using GameInterface.Services.MapTracks.Interfaces;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Library;

namespace GameInterface.Services.MapTracks.Patches;

[HarmonyPatch(typeof(MapTracksCampaignBehavior))]
internal class MapTracksCampaignBehaviorPatches
{
    [HarmonyPatch(nameof(MapTracksCampaignBehavior.GameLoadFinished))]
    [HarmonyPrefix]
    public static bool GameLoadFinishedPrefix() => ModInformation.IsServer;

    [HarmonyPatch(nameof(MapTracksCampaignBehavior.OnHourlyTick))]
    [HarmonyPrefix]
    public static bool OnHourlyTickPrefix(MapTracksCampaignBehavior __instance)
    {
        if (ModInformation.IsClient) return false;

        // Replace with interface method to run for all players
        ContainerProvider.TryResolve<IMapTracksCampaignBehaviorInterface>(out var mapTracksCampaignBehaviorInterface);

        mapTracksCampaignBehaviorInterface.OnHourlyTick(__instance);

        return false;
    }

    [HarmonyPatch(nameof(MapTracksCampaignBehavior.OnHourlyTickParty))]
    [HarmonyPrefix]
    public static bool OnHourlyTickPartyPrefix() => ModInformation.IsServer;

    [HarmonyPatch(nameof(MapTracksCampaignBehavior.OnMobilePartyDestroyed))]
    [HarmonyPrefix]
    public static bool OnMobilePartyDestroyedPrefix() => ModInformation.IsServer;

    [HarmonyPatch(nameof(MapTracksCampaignBehavior.QuarterHourlyTick))]
    [HarmonyPrefix]
    public static bool QuarterHourlyTickPrefix(MapTracksCampaignBehavior __instance)
    {
        if (ModInformation.IsClient) return false;

        // Replace with interface method to run for all players
        ContainerProvider.TryResolve<IMapTracksCampaignBehaviorInterface>(out var mapTracksCampaignBehaviorInterface);

        mapTracksCampaignBehaviorInterface.QuarterHourlyTick(__instance);

        return false;
    }

    [HarmonyPatch(nameof(MapTracksCampaignBehavior.IsTrackDropped))]
    [HarmonyPrefix]
    public static bool IsTrackDroppedPrefix(MapTracksCampaignBehavior __instance, ref bool __result, MobileParty mobileParty)
    {
        if (ModInformation.IsClient) return false;

        ContainerProvider.TryResolve<IMapTracksCampaignBehaviorInterface>(out var mapTracksCampaignBehaviorInterface);

        __result = mapTracksCampaignBehaviorInterface.IsTrackDropped(__instance, mobileParty);

        return false;
    }

    [HarmonyPatch(nameof(MapTracksCampaignBehavior.AddTrack))]
    [HarmonyPrefix]
    public static bool AddTrackPrefix(MapTracksCampaignBehavior __instance, MobileParty party, CampaignVec2 trackPosition, Vec2 trackDirection)
    {
        if (ModInformation.IsClient) return false;

        ContainerProvider.TryResolve<IMapTracksCampaignBehaviorInterface>(out var mapTracksCampaignBehaviorInterface);

        mapTracksCampaignBehaviorInterface.AddTrack(__instance, party, trackPosition, trackDirection);

        return false;
    }
}
