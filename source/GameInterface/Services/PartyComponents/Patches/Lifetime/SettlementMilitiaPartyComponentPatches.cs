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
using TaleWorlds.CampaignSystem.Settlements;

namespace GameInterface.Services.PartyComponents.Patches.Lifetime;

[HarmonyPatch(typeof(MilitiaPartyComponent))]
internal class MilitiaPartyComponentPatches
{
    private static readonly ILogger Logger = LogManager.GetLogger<MilitiaPartyComponentPatches>();

    [HarmonyPatch(nameof(MilitiaPartyComponent.Settlement), MethodType.Setter)]
    [HarmonyPrefix]
    static bool SettlementPrefix(MilitiaPartyComponent __instance, Settlement value)
    {
        // Call original if we call this function
        if (CallOriginalPolicy.IsOriginalAllowed()) return true;

        if (ModInformation.IsClient)
        {
            Logger.Error("Client created unmanaged {name}\n"
                + "Callstack: {callstack}", typeof(MilitiaPartyComponent), Environment.StackTrace);
            return false;
        }

        var message = new MilitiaPartyComponentSettlementChanged(__instance, value.StringId);

        MessageBroker.Instance.Publish(__instance, message);

        return true;
    }
}
