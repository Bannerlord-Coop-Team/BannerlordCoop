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

    [HarmonyPatch(nameof(MobilePartyAi.AiBehaviorInteractable), MethodType.Setter)]
    [HarmonyPrefix]
    internal static void AiBehaviorInteractable_Prefix(
        ref MobilePartyAi __instance,
        ref IInteractablePoint value,
        out bool __state)
    {
        __state = false;

        if (CallOriginalPolicy.IsOriginalAllowed())
            return;

        if (ModInformation.IsClient)
            return;

        __state = ShouldCaptureInteractableChange(__instance, value);
    }

    internal static bool ShouldCaptureInteractableChange(
        MobilePartyAi partyAi,
        IInteractablePoint value) =>
        partyAi?._mobileParty?.IsActive == true && value != partyAi.AiBehaviorInteractable;

    [HarmonyPatch(nameof(MobilePartyAi.AiBehaviorInteractable), MethodType.Setter)]
    [HarmonyPostfix]
    internal static void AiBehaviorInteractable_Postfix(ref MobilePartyAi __instance, bool __state)
    {
        var party = __instance._mobileParty;
        if (!__state || party?.IsActive != true)
            return;

        // A bare setter has no enclosing MobileParty.SetMove* finalizer. Feed it into the same
        // complete latest-wins snapshot path; nested movement calls are harmlessly coalesced.
        MessageBroker.Instance.Publish(
            __instance,
            new MobilePartyMovementStateChanged(party));
    }
}
