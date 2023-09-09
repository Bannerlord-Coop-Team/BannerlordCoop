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
/// Prevents decision making for remotely controlled parties.
/// </summary>
[HarmonyPatch(typeof(MobilePartyAi))]
static class PartyDecisionMakingPatch
{
    [HarmonyPrefix]
    [HarmonyPatch(nameof(MobilePartyAi.DoNotMakeNewDecisions), MethodType.Getter)]
    static bool PrefixDoNotMakeNewDecisionsGetter(ref bool __result)
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
    [HarmonyPatch(typeof(SetPartyAiAction), "ApplyInternal", MethodType.Normal)]
    static bool Prefix(ref MobileParty owner)
    {
        if (owner.Ai.DoNotMakeNewDecisions)
            return false;

        return true;
    }
}