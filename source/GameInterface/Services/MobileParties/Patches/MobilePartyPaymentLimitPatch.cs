using GameInterface.Services.MobileParties.Extensions;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.MobileParties.Patches;

[HarmonyPatch(typeof(MobileParty))]
internal class MobilePartyPaymentLimitPatch
{
    [HarmonyPatch(nameof(MobileParty.PaymentLimit), MethodType.Getter)]
    [HarmonyPrefix]
    public static bool PaymentLimitGetterPrefix(MobileParty __instance, ref int __result)
    {
        // Restore payment limits for player parties in old saves.
        // Old saves can have player parties with limited wages from before this was fixed.
        if (__instance.IsPlayerParty())
        {
            __result = Campaign.Current.Models.PartyWageModel.MaxWagePaymentLimit;
            return false;
        }

        return true;
    }
}
