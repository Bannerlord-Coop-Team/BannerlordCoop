using Common;
using Common.Messaging;
using GameInterface.Policies;
using GameInterface.Services.MobileParties.Messages;
using HarmonyLib;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.MobileParties.Patches;

[HarmonyPatch(typeof(MobileParty))]
internal class ResetCachedPatch
{
    [HarmonyPatch(nameof(MobileParty.ResetCached))]
    [HarmonyPostfix]
    public static void ResetCachedPostfix(MobileParty __instance)
    {
        // Save loading resets every party before campaign objects are registered. Those resets are
        // local initialization, not network state changes. The policy also prevents received applies
        // from echoing back through this postfix.
        if (ModInformation.IsClient || CallOriginalPolicy.IsOriginalAllowed()) return;

        var message = new ResetMobilePartyCached(__instance);
        MessageBroker.Instance.Publish(__instance, message);
    }
}
