using Common;
using Common.Logging;
using Common.Messaging;
using GameInterface.Policies;
using GameInterface.Services.PartyVisuals.Messages;
using HarmonyLib;
using SandBox.View;
using SandBox.View.Map;
using SandBox.View.Map.Visuals;
using Serilog;
using System;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.PartyVisuals.Patches;

[HarmonyPatch]
public class PartyVisualLifetimePatches
{
    private static ILogger Logger = LogManager.GetLogger<PartyVisualLifetimePatches>();

    [HarmonyPatch(typeof(MobilePartyVisual), MethodType.Constructor, typeof(PartyBase))]
    [HarmonyPrefix]
    private static bool CreatePartyVisualPrefix(ref MobilePartyVisual __instance, PartyBase partyBase)
    {
        // Call original if we call this function
        if (CallOriginalPolicy.IsOriginalAllowed()) return true;

        if (ModInformation.IsClient) return true;

        var message = new PartyVisualCreated(__instance, partyBase);

        MessageBroker.Instance.Publish(__instance, message);

        return true;
    }

    [HarmonyPatch(typeof(MobilePartyVisual), nameof(MobilePartyVisual.OnPartyRemoved))]
    [HarmonyPostfix]
    private static void OnMobilePartyDestroyedPostfix(ref MobilePartyVisual __instance)
    {
        if (CallOriginalPolicy.IsOriginalAllowed()) return;

        if (ModInformation.IsClient)
        {
            Logger.Error("Client destroyed unmanaged {name}", typeof(MobilePartyVisual));
            return;
        }

        var message = new PartyVisualDestroyed(__instance);

        MessageBroker.Instance.Publish(__instance, message);
    }
}