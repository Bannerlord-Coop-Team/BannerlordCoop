using Common;
using Common.Messaging;
using GameInterface.Policies;
using GameInterface.Services.Heroes.Extensions;
using GameInterface.Services.Kingdoms.Messages;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;

namespace GameInterface.Services.Kingdoms.Patches;

[HarmonyPatch(typeof(TradeAgreementsCampaignBehavior))]
internal class TradeAgreementsCampaignBehaviorPatches
{
    [HarmonyPatch(nameof(TradeAgreementsCampaignBehavior.OnKingdomDestroyed))]
    [HarmonyPrefix]
    public static bool OnKingdomDestroyedPrefix() =>
        CallOriginalPolicy.IsOriginalAllowed() || ModInformation.IsServer;

    [HarmonyPatch(nameof(TradeAgreementsCampaignBehavior.WarDeclared))]
    [HarmonyPrefix]
    public static bool WarDeclaredPrefix() =>
        CallOriginalPolicy.IsOriginalAllowed() || ModInformation.IsServer;

    [HarmonyPatch(nameof(TradeAgreementsCampaignBehavior.SettlementEntered))]
    [HarmonyPrefix]
    public static bool SettlementEnteredPrefix() =>
        CallOriginalPolicy.IsOriginalAllowed() || ModInformation.IsServer;

    [HarmonyPatch(nameof(TradeAgreementsCampaignBehavior.MakeTradeAgreement))]
    [HarmonyPrefix]
    public static bool MakeTradeAgreementPrefix() => 
        CallOriginalPolicy.IsOriginalAllowed() || ModInformation.IsServer;

    [HarmonyPatch(nameof(TradeAgreementsCampaignBehavior.EndTradeAgreement))]
    [HarmonyPrefix]
    public static bool EndTradeAgreementPrefix() => 
        CallOriginalPolicy.IsOriginalAllowed() || ModInformation.IsServer;

    [HarmonyPatch(nameof(TradeAgreementsCampaignBehavior.OnTradeGoldDistributedInKingdom))]
    [HarmonyPrefix]
    public static bool OnTradeGoldDistributedInKingdomPrefix() => 
        CallOriginalPolicy.IsOriginalAllowed() || ModInformation.IsServer;

    [HarmonyPatch(nameof(TradeAgreementsCampaignBehavior.SettlementEntered))]
    [HarmonyPostfix]
    public static void SettlementEnteredPostfix(TradeAgreementsCampaignBehavior __instance, MobileParty party, Settlement settlement, Hero hero)
    {
        if (ModInformation.IsClient) return;

        if (party != null
            && party.IsActive
            && party.MapFaction != null
            && party.IsCaravan
            && settlement.IsTown
            && party.MapFaction != settlement.MapFaction
            && settlement.MapFaction.IsKingdomFaction
            && party.MapFaction.IsKingdomFaction
            && party.IsPartyTradeActive
            && !party.IsCurrentlyUsedByAQuest
            && __instance.TryGetTradeAgreement((Kingdom)settlement.MapFaction, (Kingdom)party.MapFaction, out var index)
            && !party.IsFleeing())
        {
            var message = new UpdateTradeAgreement(__instance._tradeAgreements[index]);
            MessageBroker.Instance.Publish(__instance, message);
        }
    }

    [HarmonyPatch(nameof(TradeAgreementsCampaignBehavior.ApplyBrokenTradeAgreementPenalty))]
    [HarmonyPrefix]
    public static bool ApplyBrokenTradeAgreementPenaltyPrefix(Kingdom kingdom, Kingdom otherKingdom, DeclareWarAction.DeclareWarDetail detail)
    {
        // Vanilla uses a check for player hostility to apply penalty for player.
        // Player hero that caused the war to be declared isn't available here.
        Hero hero = kingdom.Leader;

        // TODO: Replace otherKingdom.Leader relation penalty with actual client declaring war
        //ChangeRelationAction.ApplyRelationChangeBetweenHeroes(hero, otherKingdom.Leader, -50, true);
        if (hero.IsPlayerHero())
        {
            // TODO: Traits not synced yet
            //TraitLevelingHelper.OnTradeAgreementBroken();
        }

        return false;
    }

    [HarmonyPatch(nameof(TradeAgreementsCampaignBehavior.AcceptOffer))]
    [HarmonyPrefix]
    public static bool AcceptOfferPrefix(TradeAgreementsCampaignBehavior __instance, Kingdom fromKingdom)
    {
        if (ModInformation.IsServer) return false;

        // Comes from client accepting offer, send to server
        var message = new ClientAcceptsTradeAgreementOffer(fromKingdom, Clan.PlayerClan.Kingdom);
        MessageBroker.Instance.Publish(__instance, message);

        return false;
    }

    [HarmonyPatch(nameof(TradeAgreementsCampaignBehavior.OnTradeGoldDistributedInKingdom))]
    [HarmonyPostfix]
    public static void OnTradeGoldDistributedInKingdomPostfix(TradeAgreementsCampaignBehavior __instance, Kingdom kingdom1, Kingdom kingdom2, Clan clan, int share)
    {
        if (ModInformation.IsClient) return;

        var message = new TradeGoldDistributedInKingdom(kingdom1, kingdom2, clan, share);
        MessageBroker.Instance.Publish(__instance, message);
    }

    [HarmonyPatch(nameof(TradeAgreementsCampaignBehavior.MakeTradeAgreement))]
    [HarmonyPostfix]
    public static void MakeTradeAgreementPostfix(TradeAgreementsCampaignBehavior __instance, Kingdom kingdom1, Kingdom kingdom2, CampaignTime duration)
    {
        if (ModInformation.IsClient) return;

        if (!__instance.TryGetTradeAgreement(kingdom1, kingdom2, out var newAgreementIndex)) return;

        var newAgreement = __instance._tradeAgreements[newAgreementIndex];

        var message = new MakeTradeAgreement(newAgreement);
        MessageBroker.Instance.Publish(__instance, message);
    }

    [HarmonyPatch(nameof(TradeAgreementsCampaignBehavior.RemoveTradeAgreement))]
    [HarmonyPostfix]
    public static void RemoveTradeAgreementPostfix(TradeAgreementsCampaignBehavior __instance, Kingdom kingdom1, Kingdom kingdom2)
    {
        if (ModInformation.IsClient) return;

        var message = new RemoveTradeAgreement(kingdom1, kingdom2);
        MessageBroker.Instance.Publish(__instance, message);
    }

    [HarmonyPatch(nameof(TradeAgreementsCampaignBehavior.EndTradeAgreementsOfKingdom))]
    [HarmonyPostfix]
    public static void EndTradeAgreementsOfKingdomPostfix(TradeAgreementsCampaignBehavior __instance, Kingdom kingdom)
    {
        if (ModInformation.IsClient) return;

        var message = new EndAllTradeAgreements(kingdom);
        MessageBroker.Instance.Publish(__instance, message);
    }
}
