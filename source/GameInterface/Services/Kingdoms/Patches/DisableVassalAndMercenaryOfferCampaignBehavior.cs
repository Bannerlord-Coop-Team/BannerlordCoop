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

[HarmonyPatch(typeof(VassalAndMercenaryOfferCampaignBehavior))]
internal class VassalAndMercenaryOfferCampaignBehaviorPatches
{
    [HarmonyPatch(nameof(VassalAndMercenaryOfferCampaignBehavior.DailyTick))]
    [HarmonyPrefix]
    public static bool DailyTickPrefix(ref VassalAndMercenaryOfferCampaignBehavior __instance)
    {
        return false; // TODO : Add vassal and mercenary offers for players. For now, disabled.
    }

    [HarmonyPatch(nameof(VassalAndMercenaryOfferCampaignBehavior.OnHeroPrisonerTaken))]
    [HarmonyPrefix]
    public static bool OnHeroPrisonerTakenPrefix(ref VassalAndMercenaryOfferCampaignBehavior __instance, PartyBase captor, Hero prisoner)
    {
        if (prisoner.IsPlayerHero() && __instance._currentMercenaryOffer != null)
        {
            __instance.CancelVassalOrMercenaryServiceOffer(__instance._currentMercenaryOffer.Item1);
            {
                foreach (Kingdom item in __instance._vassalOffers.Keys.ToList())
                {
                    __instance.CancelVassalOrMercenaryServiceOffer(item);
                }

                return false;
            }
        }

        if (prisoner.IsKingdomLeader)
        {
            __instance.CancelVassalOrMercenaryServiceOffer(prisoner.MapFaction as Kingdom);
        }

        return false;
    }

    [HarmonyPatch(nameof(VassalAndMercenaryOfferCampaignBehavior.OnClanChangedKingdom))]
    [HarmonyPrefix]
    public static bool OnClanChangedKingdomPrefix(ref VassalAndMercenaryOfferCampaignBehavior __instance, Clan clan, Kingdom oldKingdom, Kingdom newKingdom, ChangeKingdomAction.ChangeKingdomActionDetail detail, bool showNotification)
    {
        if (!clan.IsPlayerClan() || newKingdom == null)
        {
            return false;
        }

        if (detail == ChangeKingdomAction.ChangeKingdomActionDetail.JoinAsMercenary && __instance._currentMercenaryOffer != null && __instance._currentMercenaryOffer.Item1 != newKingdom)
        {
            __instance.CancelVassalOrMercenaryServiceOffer(__instance._currentMercenaryOffer.Item1);
        }
        else
        {
            if (detail != ChangeKingdomAction.ChangeKingdomActionDetail.JoinKingdom && detail != ChangeKingdomAction.ChangeKingdomActionDetail.CreateKingdom)
            {
                return false;
            }

            __instance._stopOffers = true;
            if (__instance._currentMercenaryOffer != null)
            {
                __instance.CancelVassalOrMercenaryServiceOffer(__instance._currentMercenaryOffer.Item1);
            }

            foreach (KeyValuePair<Kingdom, CampaignTime> item in __instance._vassalOffers.ToDictionary((KeyValuePair<Kingdom, CampaignTime> x) => x.Key, (KeyValuePair<Kingdom, CampaignTime> x) => x.Value))
            {
                __instance.CancelVassalOrMercenaryServiceOffer(item.Key);
            }
        }

        return false;
    }

    [HarmonyPatch(nameof(VassalAndMercenaryOfferCampaignBehavior.OnVassalOrMercenaryServiceOfferedToPlayer))]
    [HarmonyPrefix]
    public static bool OnVassalOrMercenaryServiceOfferedToPlayerPrefix(ref VassalAndMercenaryOfferCampaignBehavior __instance, Kingdom kingdom)
    {
        return false;
    }

    [HarmonyPatch(nameof(VassalAndMercenaryOfferCampaignBehavior.OnWarDeclared))]
    [HarmonyPrefix]
    public static bool OnWarDeclaredPrefix(ref VassalAndMercenaryOfferCampaignBehavior __instance, IFaction faction1, IFaction faction2, DeclareWarAction.DeclareWarDetail detail)
    {
        return false;
    }

    [HarmonyPatch(nameof(VassalAndMercenaryOfferCampaignBehavior.OnHeroRelationChanged))]
    [HarmonyPrefix]
    public static bool OnHeroRelationChangedPrefix(ref VassalAndMercenaryOfferCampaignBehavior __instance, Hero effectiveHero, Hero effectiveHeroGainedRelationWith, int relationChange, bool showNotification, ChangeRelationAction.ChangeRelationDetail detail, Hero originalHero, Hero originalGainedRelationWith)
    {
        return false;
    }

    [HarmonyPatch(nameof(VassalAndMercenaryOfferCampaignBehavior.OnKingdomDestroyed))]
    [HarmonyPrefix]
    public static bool OnKingdomDestroyedPrefix(ref VassalAndMercenaryOfferCampaignBehavior __instance, Kingdom destroyedKingdom)
    {
        return false;
    }

    [HarmonyPatch(nameof(VassalAndMercenaryOfferCampaignBehavior.OnPlayerCharacterChanged))]
    [HarmonyPrefix]
    public static bool OnPlayerCharacterChangedPrefix(ref VassalAndMercenaryOfferCampaignBehavior __instance, Hero oldPlayer, Hero newPlayer, MobileParty newMainParty, bool isMainPartyChanged)
    {
        return false;
    }
}
