﻿using Common.Messaging;
using Common.Extensions;
using GameInterface.Extentions;
using GameInterface.Services.MobileParties.Messages.Behavior;
using HarmonyLib;
using System;
using System.Reflection;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.MobileParties.Patches;

[HarmonyPatch(typeof(MobilePartyAi))]
static class DisablePartyDecisionMaking
{
    static object AllowLock = new object();
    static MobilePartyAi AllowedTickInternal;
    static Action<MobilePartyAi> TickInternal = typeof(MobilePartyAi)
        .GetMethod("TickInternal", BindingFlags.Instance | BindingFlags.NonPublic)
        .BuildDelegate<Action<MobilePartyAi>>();
    public static void TickInternalOverride(MobilePartyAi partyAi)
    {
        AllowedTickInternal = partyAi;
        lock (AllowLock)
        {
            TickInternal(partyAi);
        }
        AllowedTickInternal = null;
    }

    [HarmonyPrefix]
    [HarmonyPatch("TickInternal")]
    static bool PrefixTickInternal(ref MobilePartyAi __instance)
    {
        if (AllowedTickInternal == __instance)
        {
            return true;
        }

        if (ModInformation.IsServer || __instance.GetMobileParty().IsMainParty)
        {
            MessageBroker.Instance.Publish(__instance, new RequestTickInternal(__instance));
        }

        return false;
    }

    [HarmonyPrefix]
    [HarmonyPatch(nameof(MobilePartyAi.DoNotMakeNewDecisions), MethodType.Getter)]
    static bool PrefixDoNotMakeNewDecisionsGetter(MobilePartyAi __instance, ref bool __result)
    {
        if (ModInformation.IsClient)
        {
            // Disable decision making on clients, only the server can update the AI.
            __result = true;
            return false;
        }

        return true;
        /*MobileParty party = __instance.GetMobileParty();
        if (party.IsAnyPlayerMainParty() || ModInformation.IsClient)
        {
            // Disable decision making for parties our client doesn't control. Decisions are made remote.
            __result = true;
            return false;
        }

        return true;*/
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(SetPartyAiAction), "ApplyInternal", MethodType.Normal)]
    static bool Prefix(ref MobileParty owner)
    {
        if (owner.Ai.DoNotMakeNewDecisions || ModInformation.IsClient)
        {
            return false;
        }

        return true;
    }

    
}