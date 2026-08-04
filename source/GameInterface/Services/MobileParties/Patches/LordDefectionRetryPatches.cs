using GameInterface.Configuration;
using HarmonyLib;
using System.Collections.Generic;
using System.Reflection;
using TaleWorlds.CampaignSystem.CampaignBehaviors;

namespace GameInterface.Services.MobileParties.Patches;

/// <summary>
/// Applies the configured <see cref="LordDefectionRetryMode"/> to a lord's memory of refused
/// recruitment attempts.
/// </summary>
/// <remarks>
/// Vanilla records every persuasion attempt in the behavior's private
/// _previousDefectionPersuasionAttempts list and refuses the lord while a matching entry survives.
/// OnDailyTick prunes entries older than an in-game year, and that is the only thing that clears
/// them. The list is filled in only by the machine that ran the conversation - the client - so
/// whether the tick runs there decides how long a refusal lasts:
///
///   Vanilla     - tick runs everywhere, a refusal expires after a year (default, matches singleplayer)
///   NeverExpire - tick suppressed on clients, so a refusal lasts the whole session
///   AlwaysRetry - the list is emptied each tick, so a lord can be asked again immediately
///
/// This used to be a blanket prefix returning false, which is NeverExpire in all but name and was
/// never a deliberate choice.
/// </remarks>
[HarmonyPatch(typeof(LordDefectionCampaignBehavior))]
internal class LordDefectionRetryPatches
{
    static IEnumerable<MethodBase> TargetMethods() => new MethodBase[]
    {
        AccessTools.Method(typeof(LordDefectionCampaignBehavior), nameof(LordDefectionCampaignBehavior.OnDailyTick))
    };

    [HarmonyPrefix]
    static bool Prefix(LordDefectionCampaignBehavior __instance)
    {
        switch (ModConfigProvider.ModOptions.LordDefectionRetries)
        {
            case LordDefectionRetryMode.NeverExpire:
                // Skip the prune, so nothing an attempt recorded is ever forgotten.
                return false;

            case LordDefectionRetryMode.AlwaysRetry:
                __instance._previousDefectionPersuasionAttempts?.Clear();
                return false;

            default:
                return true;
        }
    }
}
