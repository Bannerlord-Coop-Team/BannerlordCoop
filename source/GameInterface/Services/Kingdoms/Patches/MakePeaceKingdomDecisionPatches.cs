using GameInterface.Services.Kingdoms.Extentions;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
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
/// Uses registered player kingdoms for native's ruling-clan peace support on every endpoint.
/// </summary>
[HarmonyPatch(typeof(MakePeaceKingdomDecision), nameof(MakePeaceKingdomDecision.DetermineSupport))]
internal static class MakePeaceKingdomDecisionSupportPatch
{
    [HarmonyTranspiler]
    private static IEnumerable<CodeInstruction> DetermineSupportTranspiler(IEnumerable<CodeInstruction> instructions)
    {
        var codes = instructions.ToList();
        var playerClanGetter = AccessTools.PropertyGetter(typeof(Clan), nameof(Clan.PlayerClan));
        var mapFactionGetter = AccessTools.PropertyGetter(typeof(Clan), nameof(Clan.MapFaction));
        var kingdomGetter = AccessTools.PropertyGetter(typeof(KingdomDecision), nameof(KingdomDecision.Kingdom));
        for (int i = 0; i < codes.Count - 3; i++)
        {
            if (!codes[i].Calls(playerClanGetter) || !codes[i + 1].Calls(mapFactionGetter)
                || codes[i + 2].opcode != OpCodes.Ldarg_0 || !codes[i + 3].Calls(kingdomGetter)) continue;

            // Native dereferences PlayerClan before checking whether this is an opponent's offer.
            codes[i].opcode = OpCodes.Ldarg_0;
            codes[i].operand = null;
            codes[i + 1].opcode = OpCodes.Call;
            codes[i + 1].operand = kingdomGetter;
            codes[i + 2].opcode = OpCodes.Call;
            codes[i + 2].operand = AccessTools.Method(typeof(KingdomExtensions), nameof(KingdomExtensions.IsPlayerKingdom));
            codes[i + 3].opcode = OpCodes.Ldc_I4_1;
            codes[i + 3].operand = null;
            return codes;
        }

        throw new InvalidOperationException("Could not replace the local player faction check in peace decision support.");
    }
}
