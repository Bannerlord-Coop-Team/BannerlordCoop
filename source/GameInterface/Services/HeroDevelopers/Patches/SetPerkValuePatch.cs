using GameInterface.Policies;
using GameInterface.Utils;
using HarmonyLib;
using System.Reflection;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CharacterDevelopment;

namespace GameInterface.Services.HeroDevelopers.Patches
{
    /// <summary>
    /// Broadcasts perk value changes. Hero.SetPerkValueInternal computes SetPropertyValue's int
    /// argument with a bool->int branch between the _heroPerks field load and the call, which the
    /// control-flow-safe AutoSync PropertyOwner transpiler skips - so this prefix routes the change
    /// through the same cached PropertyOwner set intercept instead, then re-fires the vanilla perk
    /// events the skipped original would have raised (the perk side-effect replication in
    /// <see cref="Handlers.PerkActivationHandler"/> hangs off OnPerkOpened).
    /// </summary>
    [HarmonyPatch(typeof(Hero))]
    internal class SetPerkValuePatch
    {
        private static readonly FieldInfo HeroPerksField = AccessTools.Field(typeof(Hero), nameof(Hero._heroPerks));

        [HarmonyPatch(nameof(Hero.SetPerkValueInternal))]
        [HarmonyPrefix]
        private static bool SetPerkValueInternalPrefix(Hero __instance, PerkObject perk, bool value)
        {
            if (CallOriginalPolicy.IsOriginalAllowed()) return true;

            var heroPerks = __instance._heroPerks;
            if (heroPerks == null) return true;

            // The AutoSync intercept is cached when the Hero PropertyOwner transpiler is applied;
            // without it there is no pipeline to broadcast through, so run vanilla untouched
            if (!GenericPatchHelpers.PropertyOwnerSetInterceptCache.TryGetValue(HeroPerksField, out var setIntercept))
                return true;

            // Vanilla early-out: perk already in the target state - no mutation, no events
            if ((heroPerks.GetPropertyValue(perk) == 1) == value) return false;

            // Applies the value and, when running as the server, publishes the generated
            // Hero __heroPerks set message; on a client it applies locally and logs the
            // unmanaged update, matching every other PropertyOwner mutation
            setIntercept.Invoke(null, new object[] { __instance, heroPerks, perk, value ? 1 : 0 });

            if (value)
                CampaignEventDispatcher.Instance?.OnPerkOpened(__instance, perk);
            else
                CampaignEventDispatcher.Instance?.OnPerkReset(__instance, perk);

            return false;
        }
    }
}
