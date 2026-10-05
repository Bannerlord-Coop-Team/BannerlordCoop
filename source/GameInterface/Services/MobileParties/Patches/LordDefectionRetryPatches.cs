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
/// Vanilla keeps TWO independent rules, and the one that reaches the player first is not the one
/// that looks like the gate:
///
///   <c>conversation_lord_from_ruling_clan_on_condition</c> - the PRE-GATE, and the real blocker. It
///                                 refuses while the lord has an unsuccessful attempt that is still in
///                                 its cooldown (<c>IsPersuiasionAttemptInCooldown</c>, ONE SEASON since
///                                 v1.5.4), unless the score says to retry or skip to the barter.
///                                 It returns before CanAttemptToPersuade is ever consulted.
///   <c>CanAttemptToPersuade</c> - the GATE. Refuses while a matching unsuccessful attempt is less than
///                                 ONE WEEK old. The active persuasion also reuses it to choose the failed
///                                 task whose final refusal line is shown.
///
/// Attempts are never removed by vanilla any more (the yearly prune is gone), so expiry is the cooldown
/// alone. Patching the gate alone cannot work, because the pre-gate already answered. AlwaysRetry
/// therefore has to drop this lord's attempt records before a new conversation - and it must drop ALL of
/// them, not just the unsuccessful ones, because the score counts successes and every persuasion OPTION
/// records its own attempt. The gate itself must keep running so a fresh failure can select its refusal line.
///
///   Vanilla     - unchanged: vanilla's own week/season rules apply (default, matches singleplayer)
///   NeverExpire - the gate blocks while ANY unsuccessful attempt survives, and attempts never leave
///                 their cooldown
///   AlwaysRetry - the pre-gate's records are cleared before each conversation, so the lord can be asked
///                 again at once while fresh failures still complete normally
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
