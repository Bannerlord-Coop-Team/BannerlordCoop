using GameInterface.Extentions;
using HarmonyLib;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Party;
using Common.Messaging;
using GameInterface.Utils;
using GameInterface.Services.MobileParties.Messages;

namespace GameInterface.Services.MobileParties.Patches;

[HarmonyPatch(typeof(MobilePartyAi))]
static class DisablePartyDecisionMaking
{
    static MobilePartyAi AllowedTickInternal;

    public static void TickInternalOverride(MobilePartyAi partyAi)
    {
        AllowedTickInternal = partyAi;
        lock (AllowedTickInternal)
        {
            ReflectionUtils.InvokePrivateMethod(typeof(MobilePartyAi), "TickInternal", partyAi);
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