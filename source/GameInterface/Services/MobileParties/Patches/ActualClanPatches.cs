using Common;
using Common.Logging;
using Common.Messaging;
using Common.Util;
using GameInterface.Policies;
using GameInterface.Services.MobileParties.Messages;
using HarmonyLib;
using Serilog;
using System;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.MobileParties.Patches;

[HarmonyPatch(typeof(MobileParty))]
internal class ActualClanPatches
{
    private static readonly ILogger Logger = LogManager.GetLogger<ActualClanPatches>();

    [HarmonyPatch(nameof(MobileParty.ActualClan), MethodType.Setter)]
    [HarmonyPrefix]
    private static bool SetActualClanPrefix(MobileParty __instance, Clan value)
    {
        // Skip if we called it
        if (CallOriginalPolicy.IsOriginalAllowed()) return true;

        if (ModInformation.IsClient)
        {
            Logger.Error("Client created unmanaged {name}\n"
                + "Callstack: {callstack}", typeof(Army), Environment.StackTrace);
            return true;
        }

        var message = new ActualClanChanged(__instance.StringId, value.StringId);
        MessageBroker.Instance.Publish(__instance, message);

        return ModInformation.IsServer;
    }

    internal static void OverrideSetActualClan(MobileParty mobileParty, Clan clan)
    {
        GameLoopRunner.RunOnMainThread(() =>
        {
            using (new AllowedThread())
            {
                mobileParty.ActualClan = clan;
            }
        });
    }
}
