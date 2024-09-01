using Common;
using Common.Logging;
using Common.Messaging;
using Common.Util;
using GameInterface.Policies;
using GameInterface.Services.Armies.Data;
using GameInterface.Services.Armies.Messages.Lifetime;
using GameInterface.Services.PartyVisuals.Messages;
using HarmonyLib;
using SandBox.View.Map;
using Serilog;
using System;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using static TaleWorlds.CampaignSystem.Army;

namespace GameInterface.Services.Armies.Patches;

/// <summary>
/// Patches required for creating an Army
/// </summary>
[HarmonyPatch]
internal class PartyVisualsLifetimePatches
{
    private static ILogger Logger = LogManager.GetLogger<PartyVisualsLifetimePatches>();

    [HarmonyPatch(typeof(PartyVisual), MethodType.Constructor, typeof(PartyBase))]
    [HarmonyPrefix]
    private static bool CreatePartyVisualPrefix(ref PartyVisual __instance, PartyBase partyBase)
    {
        // Call original if we call this function
        if (CallOriginalPolicy.IsOriginalAllowed()) return true;

        if (ModInformation.IsClient)
        {
            Logger.Error("Client created unmanaged {name}\n"
                + "Callstack: {callstack}", typeof(PartyVisual), Environment.StackTrace);
            return true;
        }

        var message = new PartyVisualCreated(__instance, partyBase);

        MessageBroker.Instance.Publish(__instance, message);

        return true;
    }

    [HarmonyPatch(typeof(PartyVisual), nameof(PartyVisual.ReleaseResources))]
    [HarmonyPrefix]
    public static void PartyVisualDestroyPostfix(PartyVisual __instance)
    {
        // Call original if we called it
        if (CallOriginalPolicy.IsOriginalAllowed()) return;

        if (ModInformation.IsClient)
        {
            Logger.Error("Client created unmanaged {name}\n"
                + "Callstack: {callstack}", typeof(PartyVisual), Environment.StackTrace);
            return;
        }

        var message = new PartyVisualDestroyed(__instance);

        MessageBroker.Instance.Publish(__instance, message);
    }

    public static void OverrideDestroyPartyVisual(PartyVisual partyVisual)
    {
        GameLoopRunner.RunOnMainThread(() =>
        {
            using (new AllowedThread())
            {
                partyVisual.ReleaseResources();
            }
        });
    }
}
