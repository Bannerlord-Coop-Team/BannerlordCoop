using Common;
using Common.Logging;
using Common.Messaging;
using Common.Util;
using GameInterface.Policies;
using GameInterface.Services.PartyComponents.Messages;
using HarmonyLib;
using Serilog;
using System;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Party.PartyComponents;

namespace GameInterface.Services.PartyComponents.Patches.Lifetime;

[HarmonyPatch(typeof(MilitiaPartyComponent))]
internal class MilitiaPartyComponentPatches
{
    private static readonly ILogger Logger = LogManager.GetLogger<MilitiaPartyComponentPatches>();

    [HarmonyPatch(nameof(MilitiaPartyComponent.OnFinalize))]
    [HarmonyPrefix]
    static bool OnFinalizePrefix(MilitiaPartyComponent __instance)
    {
        // Call original if we call this function
        if (CallOriginalPolicy.IsOriginalAllowed()) return true;

        if (ModInformation.IsClient)
        {
            Logger.Error("Client created unmanaged {name}\n"
                + "Callstack: {callstack}", typeof(MilitiaPartyComponent), Environment.StackTrace);
            return true;
        }

        var message = new MilitiaPartyComponentSettlementFinalized(__instance);

        MessageBroker.Instance.Publish(__instance, message);

        return true;
    }
}
