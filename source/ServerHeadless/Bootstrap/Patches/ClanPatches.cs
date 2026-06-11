using HarmonyLib;
using TaleWorlds.CampaignSystem;

namespace ServerHeadless.Bootstrap.Patches
{
    /// <summary>
    /// Clan AfterLoad recomputes cached aggregates (strength) via the party-strength/morale models,
    /// which read default skills/perks that the skipped module XML would provide. These values are
    /// recomputed lazily during play, so skip the eager load-time recalculation headless.
    /// </summary>
    [HarmonyPatch]
    internal class ClanPatches
    {
        [HarmonyPatch(typeof(Clan), "UpdateCurrentStrength")]
        [HarmonyPrefix]
        static bool UpdateCurrentStrengthPrefix() => false;
    }
}
