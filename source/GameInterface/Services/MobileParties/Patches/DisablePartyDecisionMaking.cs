using Common.Messaging;
using Common.Extensions;
using GameInterface.Extentions;
using GameInterface.Services.MobileParties.Messages.Behavior;
using HarmonyLib;
using System;
using System.Reflection;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.MobileParties.Patches;

/// <summary>
/// Disables decision making for parties that are not controlled locally.
/// </summary>
[HarmonyPatch(typeof(MobilePartyAi))]
static class DisablePartyDecisionMaking
{
    [HarmonyPrefix]
    [HarmonyPatch(nameof(MobilePartyAi.DoNotMakeNewDecisions), MethodType.Getter)]
    static bool PrefixDoNotMakeNewDecisionsGetter(MobilePartyAi __instance, ref bool __result)
    {
        if (ModInformation.IsClient)
        {
            // Disable AI decision making on clients, only the server can update the AI.
            __result = true;
            return false;
        }

        return true;
    }

    [HarmonyPrefix]
    [HarmonyPatch("TickInternal")]
    static bool PrefixTickInternal(ref MobilePartyAi __instance)
    {
        // Allow tick for client main parties
        if (__instance.GetMobileParty().IsMainParty)
            return true;

        // Allow tick on server if not a player party
        // DoNotMakeNewDecisions is always true on clients
        if (__instance.DoNotMakeNewDecisions == false)
            return true;

        return false;
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(SetPartyAiAction), "ApplyInternal", MethodType.Normal)]
    static bool Prefix(ref MobileParty owner)
    {
        if (owner.Ai.DoNotMakeNewDecisions)
            return false;

        return true;
    }
}