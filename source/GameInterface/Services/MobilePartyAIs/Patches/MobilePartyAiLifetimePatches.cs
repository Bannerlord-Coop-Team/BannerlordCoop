using Common;
using Common.Logging;
using Common.Messaging;
using GameInterface.Policies;
using GameInterface.Registry.Auto;
using GameInterface.Services.MobilePartyAIs.Messages;
using HarmonyLib;
using Serilog;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.MobilePartyAIs.Patches;


[HarmonyPatch]
class MobilePartyAiLifetimePatches
{
    static readonly ILogger Logger = LogManager.GetLogger<MobilePartyAiLifetimePatches>();

    [HarmonyPatch(typeof(MobilePartyAi), MethodType.Constructor, typeof(MobileParty))]
    [HarmonyPostfix]
    private static void CtorPostfix(MobilePartyAi __instance, MobileParty mobileParty)
    {
        if (CallOriginalPolicy.IsOriginalAllowed()) return;

        if (ModInformation.IsClient)
        {
            Logger.Error("Client created managed {name}", typeof(MobilePartyAi));
            return;
        }

        MessageBroker.Instance.Publish(__instance, new MobilePartyAiCreated(__instance, mobileParty));
    }

    [HarmonyPatch(typeof(MobileParty), nameof(MobileParty.RemoveParty))]
    [HarmonyPostfix]
    private static void RemoveParty_Postfix(ref MobileParty __instance)
    {
        if (CallOriginalPolicy.IsOriginalAllowed()) return;

        if (ModInformation.IsClient)
        {
            Logger.Error("Client destroyed unmanaged {name}", typeof(MobileParty));
            return;
        }

        var ai = __instance.Ai;

        MessageBroker.Instance.Publish(ai, new InstanceDestroyed<MobilePartyAi>(ai));
    }
}
