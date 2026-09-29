using Common;
using Common.Messaging;
using GameInterface.Policies;
using GameInterface.Services.MobileParties.Messages;
using HarmonyLib;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.MobileParties.Patches;

[HarmonyPatch(typeof(MobileParty))]
internal class MobilePartyDisorganizationPatch
{
    [HarmonyPatch(nameof(MobileParty.CheckIsDisorganized))]
    [HarmonyPrefix]
    private static bool CheckPrefix() => ModInformation.IsServer;

    [HarmonyPatch(nameof(MobileParty.SetDisorganized))]
    [HarmonyPrefix]
    private static bool SetPrefix() => ModInformation.IsServer || CallOriginalPolicy.IsOriginalAllowed();

    [HarmonyPatch(nameof(MobileParty.SetDisorganized))]
    [HarmonyPostfix]
    private static void SetPostfix(MobileParty __instance)
    {
        if (ModInformation.IsClient || CallOriginalPolicy.IsOriginalAllowed()) return;

        MessageBroker.Instance.Publish(__instance,
            new MobilePartyDisorganizationChanged(__instance, __instance.IsDisorganized));
    }
}
