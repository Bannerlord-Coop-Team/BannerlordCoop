using Common.Logging;
using Common.Messaging;
using Common;
using GameInterface.Policies;
using GameInterface.Services.Armies.Data;
using GameInterface.Services.Armies.Messages.Lifetime;
using HarmonyLib;
using Serilog;
using System;
using System.Collections.Generic;
using System.Text;
using TaleWorlds.CampaignSystem;
using static TaleWorlds.CampaignSystem.Army;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using GameInterface.Services.Kingdoms.Messages;

namespace GameInterface.Services.Kingdoms.Patches;

[HarmonyPatch]
internal class KingdomLifetimePatches
{
    private static ILogger Logger = LogManager.GetLogger<Kingdom>();

    [HarmonyPatch(typeof(Kingdom), MethodType.Constructor)]
    [HarmonyPrefix]
    private static bool CreateKingdomPrefix(ref Kingdom __instance)
    {
        // Run original if we called it
        if (CallOriginalPolicy.IsOriginalAllowed()) return true;

        if (ModInformation.IsClient)
        {
            Logger.Error("Client created unmanaged {name}\n"
                + "Callstack: {callstack}", typeof(Army), Environment.StackTrace);
            return true;
        }

        var message = new KingdomCreated(__instance);

        MessageBroker.Instance.Publish(__instance, message);

        return true;
    }
}
