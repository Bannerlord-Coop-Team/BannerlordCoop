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
/// Vanilla keeps THREE independent rules, and the one that reaches the player first is not the one
/// that looks like the gate:
///
///   <c>conversation_lord_from_ruling_clan_on_condition</c> - the PRE-GATE, and the real blocker. When
///                                 the accumulated score is below <c>_maximumScoreCap</c> it refuses on
///                                 <c>Any(a =&gt; a.PersuadedHero == OneToOneConversationHero)</c> and
///                                 answers "You have tried to persuade me before." That predicate checks
///                                 neither AGE nor SUCCESS, so a single prior attempt blocks forever, and
///                                 it returns before CanAttemptToPersuade is ever consulted.
///   <c>CanAttemptToPersuade</c> - the GATE. Refuses while a matching unsuccessful attempt is less than
///                                 ONE WEEK old. The active persuasion also reuses it to choose the failed
///                                 task whose final refusal line is shown.
///   <c>RemoveOldAttempts</c>    - housekeeping on the daily tick, dropping records over a YEAR old. The
///                                 only thing that ever removes an attempt, so it is what eventually
///                                 releases the pre-gate.
///
/// Patching the gate alone cannot work, because the pre-gate already answered. AlwaysRetry therefore has
/// to drop this lord's attempt records before a new conversation - and it must drop ALL of them, not just
/// the unsuccessful ones, because the pre-gate's predicate ignores success and every persuasion OPTION
/// records its own attempt. The gate itself must keep running so a fresh failure can select its refusal line.
///
///   Vanilla     - unchanged: vanilla's own week/year rules apply (default, matches singleplayer)
///   NeverExpire - the gate blocks while ANY unsuccessful attempt survives, and the prune is suppressed
///                 so one always does
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
    /// The yearly prune. Only NeverExpire suppresses it, to keep the records its gate depends on.
    /// </summary>
    /// <remarks>
    /// This is the ONLY prefix on OnDailyTick. DisableLordDefectionCampaignBehavior used to add a second
    /// one returning <c>ModInformation.IsServer</c>, and because Harmony skips the original as soon as any
    /// prefix returns false, that one silently won on clients: the prune never ran there, and since the
    /// pre-gate is released only by a record being removed, a refusal lasted the whole session no matter
    /// which mode was configured. Blocking it bought nothing - OnDailyTick's entire body is a call to
    /// RemoveOldAttempts, which prunes the client's own local list and replicates nothing.
    /// </remarks>
    [HarmonyPatch(typeof(LordDefectionCampaignBehavior),
        nameof(LordDefectionCampaignBehavior.OnDailyTick))]
    internal class OnDailyTickPatch
    {
        [HarmonyPrefix]
        private static bool Prefix()
        {
            return ModConfigProvider.ModOptions.LordDefectionRetries != LordDefectionRetryMode.NeverExpire;
        }
    }
}
