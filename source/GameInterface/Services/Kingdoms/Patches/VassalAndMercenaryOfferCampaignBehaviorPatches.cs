using GameInterface.Services.Clans.Extensions;
using GameInterface.Services.Heroes.Extensions;
using HarmonyLib;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.Kingdoms.Patches;

/// <summary>
/// Makes the vassal/mercenary offer behavior co-op aware instead of switching it off wholesale.
/// </summary>
/// <remarks>
/// This used to prefix RegisterEvents with false, which stopped the behavior subscribing to anything.
/// That is heavier than it needs to be: the parts that are genuinely wrong in co-op are the ones
/// vanilla writes against a single player, because every one of them reads Hero.MainHero or
/// Clan.PlayerClan and so would act for whoever happens to be the host rather than the player the
/// event is about.
///
/// The behavior now registers normally, and each handler is replaced: the ones that only make sense
/// for one player are no-ops, and the two that keep offer bookkeeping consistent are reimplemented
/// against the co-op IsPlayerHero()/IsPlayerClan() checks, which are true for any connected player.
///
/// Offers to players stay disabled. Presenting one is a per-player decision popup with no co-op
/// routing yet, so a re-enabled DailyTick would raise offers the server cannot deliver.
/// </remarks>
[HarmonyPatch(typeof(VassalAndMercenaryOfferCampaignBehavior))]
internal class VassalAndMercenaryOfferCampaignBehaviorPatches
{
    /// <summary>Offers to players are not routed through co-op yet, so none are generated.</summary>
    [HarmonyPatch(nameof(VassalAndMercenaryOfferCampaignBehavior.DailyTick))]
    [HarmonyPrefix]
    public static bool DailyTickPrefix() => false;

    /// <summary>
    /// Cancels outstanding offers when a player, or a kingdom's leader, is captured.
    /// </summary>
    /// <remarks>
    /// Vanilla asks whether the prisoner is <c>Hero.MainHero</c>; in co-op the capture may be of any
    /// connected player, so ask IsPlayerHero() instead. Without this, a captured client leaves its
    /// pending offers alive.
    /// </remarks>
    [HarmonyPatch(nameof(VassalAndMercenaryOfferCampaignBehavior.OnHeroPrisonerTaken))]
    [HarmonyPrefix]
    public static bool OnHeroPrisonerTakenPrefix(
        VassalAndMercenaryOfferCampaignBehavior __instance, PartyBase captor, Hero prisoner)
    {
        if (prisoner.IsPlayerHero() && __instance._currentMercenaryOffer != null)
        {
            __instance.CancelVassalOrMercenaryServiceOffer(__instance._currentMercenaryOffer.Item1);

            foreach (var kingdom in __instance._vassalOffers.Keys.ToList())
                __instance.CancelVassalOrMercenaryServiceOffer(kingdom);

            return false;
        }

        if (prisoner.IsKingdomLeader)
            __instance.CancelVassalOrMercenaryServiceOffer(prisoner.MapFaction as Kingdom);

        return false;
    }

    /// <summary>
    /// Clears offers once a player clan settles into a kingdom of its own.
    /// </summary>
    /// <remarks>
    /// Same substitution: vanilla gates on Clan.PlayerClan, which in co-op is only the host's clan.
    /// </remarks>
    [HarmonyPatch(nameof(VassalAndMercenaryOfferCampaignBehavior.OnClanChangedKingdom))]
    [HarmonyPrefix]
    public static bool OnClanChangedKingdomPrefix(
        VassalAndMercenaryOfferCampaignBehavior __instance,
        Clan clan,
        Kingdom oldKingdom,
        Kingdom newKingdom,
        ChangeKingdomAction.ChangeKingdomActionDetail detail,
        bool showNotification)
    {
        if (!clan.IsPlayerClan() || newKingdom == null) return false;

        if (detail == ChangeKingdomAction.ChangeKingdomActionDetail.JoinAsMercenary &&
            __instance._currentMercenaryOffer != null &&
            __instance._currentMercenaryOffer.Item1 != newKingdom)
        {
            __instance.CancelVassalOrMercenaryServiceOffer(__instance._currentMercenaryOffer.Item1);
            return false;
        }

        if (detail != ChangeKingdomAction.ChangeKingdomActionDetail.JoinKingdom &&
            detail != ChangeKingdomAction.ChangeKingdomActionDetail.CreateKingdom)
        {
            return false;
        }

        __instance._stopOffers = true;
        if (__instance._currentMercenaryOffer != null)
            __instance.CancelVassalOrMercenaryServiceOffer(__instance._currentMercenaryOffer.Item1);

        foreach (var offer in new Dictionary<Kingdom, CampaignTime>(__instance._vassalOffers))
            __instance.CancelVassalOrMercenaryServiceOffer(offer.Key);

        return false;
    }

    /// <summary>Presenting an offer is a single-player popup; suppressed until co-op routes it.</summary>
    [HarmonyPatch(nameof(VassalAndMercenaryOfferCampaignBehavior.OnVassalOrMercenaryServiceOfferedToPlayer))]
    [HarmonyPrefix]
    public static bool OnVassalOrMercenaryServiceOfferedToPlayerPrefix() => false;

    /// <summary>War only reshapes offers that are never generated, so this has nothing to do.</summary>
    [HarmonyPatch(nameof(VassalAndMercenaryOfferCampaignBehavior.OnWarDeclared))]
    [HarmonyPrefix]
    public static bool OnWarDeclaredPrefix() => false;
}
