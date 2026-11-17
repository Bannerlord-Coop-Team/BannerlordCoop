using GameInterface.Configuration;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;

namespace GameInterface.Services.MobileParties.Patches;

/// <summary>
/// Applies the configured <see cref="LordDefectionRetryMode"/> to a lord's memory of refused
/// recruitment attempts.
/// </summary>
/// <remarks>
/// Vanilla's pre-gate, <c>conversation_lord_from_ruling_clan_on_condition</c>, refuses for a season after a failed
/// attempt before <c>CanAttemptToPersuade</c> runs, so AlwaysRetry clears all of the lord's attempts before a
/// conversation: the score counts successes too, and each persuasion option records its own attempt.
/// </remarks>
internal static class LordDefectionRetryPatches
{
    /// <summary>
    /// Keeps unsuccessful attempts blocking indefinitely for <see cref="LordDefectionRetryMode.NeverExpire"/>.
    /// </summary>
    [HarmonyPatch(typeof(LordDefectionCampaignBehavior), "CanAttemptToPersuade",
        new[] { typeof(Hero), typeof(int) })]
    internal class CanAttemptToPersuadePatch
    {
        [HarmonyPrefix]
        internal static bool Prefix(
            LordDefectionCampaignBehavior __instance, Hero targetHero, int reservationType, ref bool __result)
        {
            switch (ModConfigProvider.ModOptions.LordDefectionRetries)
            {
                case LordDefectionRetryMode.NeverExpire:
                    // Age is deliberately ignored: any surviving refusal keeps blocking.
                    __result = !HasUnsuccessfulAttempt(__instance, targetHero, reservationType);
                    return false;

                default:
                    // Vanilla also uses this check to select the current attempt's failure line.
                    return true;
            }
        }

        private static bool HasUnsuccessfulAttempt(
            LordDefectionCampaignBehavior behavior, Hero targetHero, int reservationType)
        {
            var attempts = behavior._previousDefectionPersuasionAttempts;
            if (attempts == null) return false;

            foreach (var attempt in attempts)
            {
                if (!attempt.Matches(targetHero, reservationType)) continue;
                if (attempt.IsSuccesful()) continue;

                return true;
            }

            return false;
        }
    }

    /// <summary>
    /// Clears this lord's attempt records so the pre-gate has nothing to refuse on.
    /// </summary>
    /// <remarks>
    /// Only AlwaysRetry touches this. Vanilla and NeverExpire want the records read exactly as they are:
    /// vanilla so the stock week/year rules apply, NeverExpire so the refusal stands.
    ///
    /// Clearing the records rather than forcing the condition's result is deliberate - the method also
    /// rebuilds <c>_allReservations</c> and answers several unrelated branches, so it has to run. With
    /// this lord's attempts gone the score sums to zero and the refusal predicate finds nothing, which is
    /// the same state a lord who was never approached is in. That is what "ask again at once" means.
    /// </remarks>
    [HarmonyPatch(typeof(LordDefectionCampaignBehavior),
        "conversation_lord_from_ruling_clan_on_condition")]
    internal class ConversationLordFromRulingClanPatch
    {
        [HarmonyPrefix]
        private static void Prefix(LordDefectionCampaignBehavior __instance) => ClearAttemptsForRetry(
            __instance,
            Hero.OneToOneConversationHero,
            ModConfigProvider.ModOptions.LordDefectionRetries);

        /// <summary>
        /// Takes the lord and the mode as arguments rather than reading
        /// <see cref="Hero.OneToOneConversationHero"/>, which is getter-only and so cannot be driven
        /// from a test.
        /// </summary>
        internal static void ClearAttemptsForRetry(
            LordDefectionCampaignBehavior behavior, Hero lord, LordDefectionRetryMode mode)
        {
            if (mode != LordDefectionRetryMode.AlwaysRetry) return;

            var attempts = behavior?._previousDefectionPersuasionAttempts;
            if (attempts == null || lord == null) return;

            // Deliberately NOT filtered by IsSuccesful(): the pre-gate's own predicate ignores success,
            // and every persuasion option records its own attempt, so leaving the successful ones behind
            // would let a failed persuasion keep refusing on the strength of its own partial wins.
            attempts.RemoveAll(attempt => attempt.PersuadedHero == lord);
        }
    }

    /// <summary>
    /// Keeps every refusal in its cooldown for <see cref="LordDefectionRetryMode.NeverExpire"/>, so the
    /// pre-gate keeps answering with it for the rest of the session.
    /// </summary>
    [HarmonyPatch(typeof(LordDefectionCampaignBehavior),
        nameof(LordDefectionCampaignBehavior.IsPersuiasionAttemptInCooldown))]
    internal class IsPersuasionAttemptInCooldownPatch
    {
        [HarmonyPrefix]
        private static bool Prefix(ref bool __result)
        {
            if (ModConfigProvider.ModOptions.LordDefectionRetries != LordDefectionRetryMode.NeverExpire)
                return true;

            __result = true;
            return false;
        }
    }
}
