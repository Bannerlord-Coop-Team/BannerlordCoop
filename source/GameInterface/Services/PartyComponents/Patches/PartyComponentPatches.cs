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

namespace GameInterface.Services.PartyComponents.Patches;

[HarmonyPatch(typeof(PartyComponent))]
internal class PartyComponentPatches
{
    private static readonly ILogger Logger = LogManager.GetLogger<PartyComponentPatches>();

    [HarmonyPatch(nameof(PartyComponent.MobileParty), MethodType.Setter)]
    private static bool Prefix(PartyComponent __instance, MobileParty value)
    {
        // Call original if we call this function
        if (CallOriginalPolicy.IsOriginalAllowed()) return true;

        if (ModInformation.IsClient)
        {
            Logger.Error("Client created unmanaged {name}\n"
                + "Callstack: {callstack}", typeof(PartyComponent), Environment.StackTrace);
            return false;
        }

        var message = new PartyComponentMobilePartyChanged(__instance, value);

        MessageBroker.Instance.Publish(__instance, message);

        return true;
    }

    public static void OverrideSetParty(PartyComponent component, MobileParty party)
    {
        GameLoopRunner.RunOnMainThread(() =>
        {
            using (new AllowedThread())
            {
                component.MobileParty = party;
            }
        });
    }
}
