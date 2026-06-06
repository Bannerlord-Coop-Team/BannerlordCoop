using Common;
using Common.Logging;
using Common.Messaging;
using GameInterface.Policies;
using GameInterface.Services.MobilePartyAIs.Messages;
using HarmonyLib;
using Serilog;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Map;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.MobilePartyAIs.Patches;

[HarmonyPatch(typeof(MobilePartyAi))]
internal class MobilePartyAIPatches
{
    private static readonly ILogger Logger = LogManager.GetLogger<ILogger>();

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
    static void AiBehaviorInteractable_Prefix(ref MobilePartyAi __instance, ref IInteractablePoint value)
    {
        if (CallOriginalPolicy.IsOriginalAllowed())
            return;

        if (ModInformation.IsClient)
            return;

        MessageBroker.Instance.Publish(__instance, new AiBehaviorInteractablePointUpdated(__instance, value));
    }
}
