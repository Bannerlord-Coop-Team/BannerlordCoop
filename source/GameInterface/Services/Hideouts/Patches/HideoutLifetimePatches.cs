using Common.Logging;
using Common.Messaging;
using GameInterface.Policies;
using GameInterface.Services.Hideouts.Messages;
using GameInterface.Services.Kingdoms.Messages;
using GameInterface.Services.Kingdoms.Patches;
using HarmonyLib;
using Serilog;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;

namespace GameInterface.Services.Hideouts.Patches;

[HarmonyPatch]
internal class HideoutLifetimePatches
{
    private static ILogger Logger = LogManager.GetLogger<KingdomLifetimePatches>();

    private static IEnumerable<MethodBase> TargetMethods() => AccessTools.GetDeclaredConstructors(typeof(Hideout));

    private static bool Prefix(Hideout __instance)
    {
        // Run original if we called it
        if (CallOriginalPolicy.IsOriginalAllowed()) return true;

        if (ModInformation.IsClient)
        {
            Logger.Error("Client created unmanaged {name}\n"
                + "Callstack: {callstack}", typeof(Hideout), Environment.StackTrace);
            return true;
        }

        var message = new HideoutCreated(__instance);

        MessageBroker.Instance.Publish(__instance, message);

        return true;
    }
}
