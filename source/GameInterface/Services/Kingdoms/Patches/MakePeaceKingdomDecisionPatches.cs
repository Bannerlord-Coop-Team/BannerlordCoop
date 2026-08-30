using GameInterface.Services.Kingdoms.Extentions;
using HarmonyLib;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Election;

namespace GameInterface.Services.Kingdoms.Patches;

/// <summary>
/// Keeps a synchronized inbound player peace offer pending until it is voted on or expires.
/// Native exempts only the process-local <see cref="Clan.PlayerClan"/> from reconsidering its
/// proposal, which does not identify co-op player clans on the authoritative server.
/// </summary>
[HarmonyPatch(typeof(KingdomDecision), nameof(KingdomDecision.ShouldBeCancelled))]
internal static class PendingPlayerPeaceOfferCancellationPatch
{
    [HarmonyPrefix]
    private static bool ShouldBeCancelledPrefix(KingdomDecision __instance, ref bool __result)
    {
        if (CoopKingdomElection.IsPendingPlayerPeaceOffer(__instance))
        {
            var peaceDecision = (MakePeaceKingdomDecision)__instance;
            __result = __instance.Kingdom.IsEliminated
                       || __instance.ProposerClan?.Kingdom != __instance.Kingdom
                       || !__instance.IsAllowed()
                       || peaceDecision.FactionToMakePeaceWith == null
                       || peaceDecision.FactionToMakePeaceWith.IsEliminated
                       || !__instance.Kingdom.IsAtWarWith(peaceDecision.FactionToMakePeaceWith);
            return false;
        }
        if (CoopKingdomElection.IsPendingPlayerAllianceOffer(__instance))
        {
            var allianceDecision = (StartAllianceDecision)__instance;
            __result = __instance.Kingdom.IsEliminated
                || __instance.ProposerClan?.Kingdom != __instance.Kingdom
                || !__instance.IsAllowed()
                || allianceDecision.KingdomToStartAllianceWith == null
                || allianceDecision.KingdomToStartAllianceWith.IsEliminated
                || __instance.Kingdom.IsAtWarWith(allianceDecision.KingdomToStartAllianceWith);
            return false;
        }
        return true;
    }
}

/// <summary>
/// Reproduces native's ruling-clan support for an inbound player peace offer without relying on
/// the process-local <see cref="Clan.PlayerClan"/>, which is absent on a dedicated server.
/// </summary>
[HarmonyPatch(typeof(MakePeaceKingdomDecision), nameof(MakePeaceKingdomDecision.DetermineSupport))]
internal static class PendingPlayerPeaceOfferSupportPatch
{
    [HarmonyPrefix]
    private static bool DetermineSupportPrefix(
        MakePeaceKingdomDecision __instance,
        Clan clan,
        DecisionOutcome possibleOutcome,
        ref float __result)
    {
        if (!CoopKingdomElection.IsPendingPlayerPeaceOffer(__instance)
            || clan != __instance.Kingdom.RulingClan
            || possibleOutcome is not MakePeaceKingdomDecision.MakePeaceDecisionOutcome peaceOutcome)
        {
            return true;
        }

        __result = peaceOutcome.ShouldPeaceBeDeclared ? 200f : 0f;
        return false;
    }
}
