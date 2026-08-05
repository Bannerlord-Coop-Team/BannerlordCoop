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
/// Vanilla keeps two independent rules, and only one of them decides whether a lord may be asked again:
///
///   <c>CanAttemptToPersuade</c> - the GATE. Refuses while a matching unsuccessful attempt is less than
///                                 ONE WEEK old. This is what actually blocks the player.
///   <c>RemoveOldAttempts</c>    - housekeeping on the daily tick, dropping records over a YEAR old.
///
/// Both modes therefore have to change the gate. Changing only the daily prune - which is what this did
/// before - cannot work: NeverExpire left the record in place but the gate stopped blocking after a week
/// regardless, and AlwaysRetry cleared the list only when the next daily tick happened to run, so "retry
/// immediately" still meant waiting up to a day.
///
///   Vanilla     - unchanged: a refusal blocks for one week (default, matches singleplayer)
///   NeverExpire - the gate blocks while ANY unsuccessful attempt survives, and the prune is suppressed
///                 so one always does
///   AlwaysRetry - the gate never blocks, so the lord can be asked again at once
/// </remarks>
internal static class LordDefectionRetryPatches
{
    /// <summary>
    /// The gate vanilla consults before offering the persuasion option.
    /// </summary>
    [HarmonyPatch(typeof(LordDefectionCampaignBehavior), "CanAttemptToPersuade",
        new[] { typeof(Hero), typeof(int) })]
    internal class CanAttemptToPersuadePatch
    {
        [HarmonyPrefix]
        private static bool Prefix(
            LordDefectionCampaignBehavior __instance, Hero targetHero, int reservationType, ref bool __result)
        {
            switch (ModConfigProvider.ModOptions.LordDefectionRetries)
            {
                case LordDefectionRetryMode.NeverExpire:
                    // Age is deliberately ignored: any surviving refusal keeps blocking.
                    __result = !HasUnsuccessfulAttempt(__instance, targetHero, reservationType);
                    return false;

                case LordDefectionRetryMode.AlwaysRetry:
                    __result = true;
                    return false;

                default:
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
    /// The yearly prune. Only NeverExpire touches it, and only to keep the records its gate depends on.
    /// </summary>
    [HarmonyPatch(typeof(LordDefectionCampaignBehavior),
        nameof(LordDefectionCampaignBehavior.OnDailyTick))]
    internal class OnDailyTickPatch
    {
        [HarmonyPrefix]
        private static bool Prefix()
        {
            // Suppressed so nothing a refusal recorded is ever forgotten; the gate above reads it forever.
            // AlwaysRetry lets the prune run normally - its gate ignores the list, and leaving vanilla to
            // tidy up keeps the list from growing for the whole session.
            return ModConfigProvider.ModOptions.LordDefectionRetries != LordDefectionRetryMode.NeverExpire;
        }
    }
}
