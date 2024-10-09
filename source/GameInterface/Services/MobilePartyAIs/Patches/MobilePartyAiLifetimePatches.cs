using Common.Logging;
using Common.Messaging;
using GameInterface.Policies;
using GameInterface.Services.MobilePartyAIs.Messages;
using HarmonyLib;
using Serilog;
using System;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.MobilePartyAIs.Patches;


[HarmonyPatch]
class MobilePartyAiLifetimePatches
{
    static readonly ILogger Logger = LogManager.GetLogger<MobilePartyAiLifetimePatches>();

    [HarmonyPatch(typeof(MobilePartyAi), MethodType.Constructor, typeof(MobileParty))]
    [HarmonyPrefix]
    static void CtorPrefix(MobilePartyAi __instance, MobileParty mobileParty)
    {
        // Call original if we call this function
        if (CallOriginalPolicy.IsOriginalAllowed()) return;

        if (ModInformation.IsClient)
        {
            Logger.Error("Client created unmanaged {name}\n"
                + "Callstack: {callstack}", typeof(MobileParty), Environment.StackTrace);

            return;
        }

        MessageBroker.Instance.Publish(__instance, new MobilePartyAiCreated(__instance, mobileParty));

        return;
    }

    [HarmonyPatch(typeof(MobileParty), nameof(MobileParty.RemoveParty))]
    [HarmonyPostfix]
    private static void RemoveParty_Postfix(ref MobileParty __instance)
    {
        // Call original if we call this function
        if (CallOriginalPolicy.IsOriginalAllowed()) return;

        if (ModInformation.IsClient)
        {
            Logger.Error("Client destroyed unmanaged {name}\n"
                + "Callstack: {callstack}", typeof(MobileParty), Environment.StackTrace);
            return;
        }

        var ai = __instance.Ai;

        MessageBroker.Instance.Publish(ai, new MobilePartyAiDestroyed(ai));
    }
}
