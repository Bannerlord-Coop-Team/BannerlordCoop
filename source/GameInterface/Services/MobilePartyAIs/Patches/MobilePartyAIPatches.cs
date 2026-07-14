using Common;
using Common.Messaging;
using GameInterface.Policies;
using GameInterface.Services.MobileParties.Messages.Behavior;
using HarmonyLib;
using TaleWorlds.CampaignSystem.Map;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.MobilePartyAIs.Patches;

[HarmonyPatch(typeof(MobilePartyAi))]
internal class MobilePartyAIPatches
{
    [HarmonyPatch(nameof(MobilePartyAi.CheckPartyNeedsUpdate))]
    [HarmonyPrefix]
    static void Prefix(ref MobilePartyAi __instance)
    {
        // Default path on server
        if (ModInformation.IsServer) return;

        if (__instance._mobileParty != MobileParty.MainParty)
        {
            // Disable all parties that are not the player
            __instance.DefaultBehaviorNeedsUpdate = false;
            return;
        }

        __instance.DefaultBehaviorNeedsUpdate = true;
    }

    [HarmonyPatch(nameof(MobilePartyAi.AiBehaviorInteractable), MethodType.Setter), HarmonyPrefix]
    private static void AiBehaviorInteractablePrefix(MobilePartyAi __instance, IInteractablePoint value, out bool __state) =>
        __state = ModInformation.IsServer &&
            !CallOriginalPolicy.IsOriginalAllowed() &&
            __instance?._mobileParty?.IsActive == true &&
            value != __instance.AiBehaviorInteractable;

    [HarmonyPatch(nameof(MobilePartyAi.AiBehaviorInteractable), MethodType.Setter), HarmonyPostfix]
    private static void AiBehaviorInteractablePostfix(MobilePartyAi __instance, bool __state)
    {
        if (__state)
            MessageBroker.Instance.Publish(
                __instance,
                new PartyBehaviorChangeAttempted(__instance._mobileParty));
    }
}
