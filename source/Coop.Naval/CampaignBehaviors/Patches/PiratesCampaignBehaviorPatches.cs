using Common;
using GameInterface.Extentions;
using HarmonyLib;
using NavalDLC.CampaignBehaviors;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;

namespace Coop.Naval.CampaignBehaviors.Patches;

[HarmonyPatch(typeof(PiratesCampaignBehavior))]
internal class PiratesCampaignBehaviorPatches
{
    [HarmonyPatch(nameof(PiratesCampaignBehavior.DailyTickClan))]
    [HarmonyPrefix]
    public static bool DailyTickClanPrefix() => ModInformation.IsServer;

    [HarmonyPatch(nameof(PiratesCampaignBehavior.DailyTickParty))]
    [HarmonyPrefix]
    public static bool DailyTickPartyPrefix() => ModInformation.IsServer;

    [HarmonyPatch(nameof(PiratesCampaignBehavior.AiHourlyTick))]
    [HarmonyPrefix]
    public static bool AiHourlyTickPrefix() => ModInformation.IsServer;

    [HarmonyPatch(nameof(PiratesCampaignBehavior.MapEventEnded))]
    [HarmonyPrefix]
    public static bool MapEventEndedPrefix() => ModInformation.IsServer;

    [HarmonyPatch(nameof(PiratesCampaignBehavior.OnMobilePartyDestroyed))]
    [HarmonyPrefix]
    public static bool OnMobilePartyDestroyedPrefix() => ModInformation.IsServer;

    [HarmonyPatch(nameof(PiratesCampaignBehavior.OnGameLoaded))]
    [HarmonyPrefix]
    public static bool OnGameLoadedPrefix() => ModInformation.IsServer;

    [HarmonyPatch(nameof(PiratesCampaignBehavior.TryRemoveWeakPirate))]
    [HarmonyPrefix]
    public static bool TryRemoveWeakPiratePrefix(MobileParty pirateParty)
    {
        if (!pirateParty.HasNavalNavigationCapability || pirateParty.MapEvent != null) return false;

        var partyTemplate = pirateParty.ActualClan.DefaultPartyTemplate;
        bool isWeak = pirateParty.MemberRoster.TotalHealthyCount < partyTemplate.GetLowerTroopLimit() * 0.7f
            || pirateParty.Ships.Count < partyTemplate.ShipHulls.Sum(shipHull => shipHull.MinValue) * 0.7f;
        if (!isWeak) return false;

        // Replace MobileParty.MainParty usage, weak pirates are only disbanded when no player can see them
        if (IsSeenByAnyPlayer(pirateParty.Position, 2f, 0f)) return false;

        DestroyPartyAction.ApplyForDisbanding(pirateParty, Settlement.FindFirst(settlement => settlement.IsHideout));
        return false;
    }

    [HarmonyPatch(nameof(PiratesCampaignBehavior.IsPointVisibleToPlayer))]
    [HarmonyPrefix]
    public static bool IsPointVisibleToPlayerPrefix(PiratesCampaignBehavior.PatrolZone zone, ref bool __result)
    {
        // Replace MobileParty.MainParty usage, patrol zones a player can see are skipped when spawning or reassigning pirates
        __result = IsSeenByAnyPlayer(zone.Position, 1f, zone.Radius);
        return false;
    }

    private static bool IsSeenByAnyPlayer(CampaignVec2 position, float seeingRangeMultiplier, float extraRange)
    {
        return Campaign.Current.CampaignObjectManager.GetPlayerMobileParties().Any(party =>
        {
            var range = (party.SeeingRange * seeingRangeMultiplier) + extraRange;
            return party.Position.DistanceSquared(position) < range * range;
        });
    }
}
