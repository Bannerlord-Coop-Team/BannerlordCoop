using Common;
using GameInterface.Extentions;
using GameInterface.Services.MapTracks.Interfaces;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;
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

        float skipTrackChance = Campaign.Current.Models.MapTrackModel.GetSkipTrackChance(mobileParty);
        if (MBRandom.RandomFloat < skipTrackChance)
        {
            __result = false;
            return false;
        }

        // Find the closest party to determine if the track should be dropped
        // Safe to use server MainParty as baseline as MainParty.IsActive is false
        MobileParty closestParty = MobileParty.MainParty;
        foreach (var playerParty in Campaign.Current.CampaignObjectManager.GetPlayerMobileParties())
        {
            if (mobileParty.Position.DistanceSquared(playerParty.Position) < mobileParty.Position.DistanceSquared(closestParty.Position))
            {
                closestParty = playerParty;
            }
        }

        float closestPlayerPartyDistance = mobileParty.Position.DistanceSquared(closestParty.Position);
        float num2 = closestParty.IsActive ? (closestParty._lastCalculatedSpeed * Campaign.Current.Models.MapTrackModel.MaxTrackLife) : 0f;
        __result = num2 * num2 > closestPlayerPartyDistance;

        return false;
    }
}

[HarmonyPatch(typeof(MapTracksCampaignBehavior.TrackPool))]
internal class TrackPoolPatches
{
    [HarmonyPatch(nameof(MapTracksCampaignBehavior.TrackPool.RequestTrack))]
    [HarmonyPostfix]
    public static void RequestTrackPostfix(Track __result)
    {
        // Overriden by client when received
        __result.IsEnemy = false;
        __result.IsPointer = false;
        __result.IsDetected = false;
    }
}
