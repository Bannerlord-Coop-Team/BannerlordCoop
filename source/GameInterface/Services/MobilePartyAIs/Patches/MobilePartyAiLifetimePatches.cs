using Common;
using Common.Logging;
using Common.Messaging;
using GameInterface.Policies;
using GameInterface.Registry.Auto;
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

    //[HarmonyPatch(typeof(MobilePartyAi), MethodType.Constructor, typeof(MobileParty))]
    //[HarmonyPrefix]
    //static void CtorPrefix(MobilePartyAi __instance, MobileParty mobileParty)
    //{
    //    // Call original if we call this function
    //    if (CallOriginalPolicy.IsOriginalAllowed()) return;

    //    if (ModInformation.IsClient)
    //    {
    //        Logger.Error("Client created managed {name}", typeof(MobileParty));

    //        return;
    //    }

    //    MessageBroker.Instance.Publish(__instance, new InstanceCreated<MobilePartyAi>(__instance, mobileParty));

    //    return;
    //}

    [HarmonyPatch(typeof(MobileParty), nameof(MobileParty.RemoveParty))]
    [HarmonyPrefix]
    private static void RemoveParty_Prefix(MobileParty __instance, ref bool __state)
    {
        // Capture liveness before removal: RemoveParty deactivates the party, and a second
        // removal of an already-inactive party (vanilla re-running an action the replication
        // layer already applied) must not replicate the Ai destruction again.
        __state = __instance.IsActive;
    }

    [HarmonyPatch(typeof(MobileParty), nameof(MobileParty.RemoveParty))]
    [HarmonyPostfix]
    internal static void RemoveParty_Postfix(ref MobileParty __instance, bool __state)
    {
        if (CallOriginalPolicy.IsOriginalAllowed())
        {
            // A removal nested inside another replicated action on the server (running under
            // AllowedThread) still has to deregister and replicate the party's Ai, matching the
            // normal path below; otherwise server and clients keep a dead Ai registered.
            if (CallOriginalPolicy.IsServerNestedCall() && __state)
                MessageBroker.Instance.Publish(__instance.Ai, new InstanceDestroyed<MobilePartyAi>(__instance.Ai));

            return;
        }

        if (ModInformation.IsClient)
        {
            Logger.Error("Client destroyed unmanaged {name}", typeof(MobileParty));
            return;
        }

        if (!__state) return;

        var ai = __instance.Ai;

        MessageBroker.Instance.Publish(ai, new InstanceDestroyed<MobilePartyAi>(ai));
    }
}
