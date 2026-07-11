using HarmonyLib;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Localization;

namespace GameInterface.Services.MobileParties.Patches;

[HarmonyPatch]
internal class GetBehaviorsTextPatches
{
    [HarmonyPatch(typeof(MobileParty), nameof(MobileParty.GetBehaviorText))]
    [HarmonyPrefix]
    private static bool GetBehaviorTextPrefix(MobileParty __instance, ref TextObject __result)
    {
        var army = __instance.Army;
        if (army != null &&
            army.LeaderParty != __instance &&
            !__instance.IsEngaging &&
            !__instance.IsFleeing() &&
            __instance.AttachedTo == null)
        {
            __result = new TextObject("{=OpzzCPiP}Following {TARGET_PARTY}.", null);
            __result.SetTextVariable("TARGET_PARTY", army.LeaderParty.Name);
            return false;
        }
        return true;
    }
}
