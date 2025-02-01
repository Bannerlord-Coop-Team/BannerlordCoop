using Common.Logging;
using Common.Messaging;
using GameInterface.Policies;
using GameInterface.Services.BesiegerCamps.Messages;
using HarmonyLib;
using Serilog;
using System;
using System.Collections.Generic;
using System.Reflection;
using TaleWorlds.CampaignSystem.Siege;

namespace GameInterface.Services.BesiegerCamps.Patches;

[HarmonyPatch]
internal class BesiegerCampCreationPatches
{
    private static readonly ILogger Logger = LogManager.GetLogger<BesiegerCampCreationPatches>();

    private static IEnumerable<MethodBase> TargetMethods() => AccessTools.GetDeclaredConstructors(typeof(BesiegerCamp));

    private static bool Prefix(ref BesiegerCamp __instance)
    {
        // Call original if we call this function
        if (CallOriginalPolicy.IsOriginalAllowed()) return true;

        if (ModInformation.IsClient)
        {
            Logger.Error("Client created unmanaged {name}\n"
                + "Callstack: {callstack}", typeof(BesiegerCamp), Environment.StackTrace);
            return true;
        }

        var message = new BesiegerCampCreated(__instance);

        MessageBroker.Instance.Publish(__instance, message);

        return true;
    }
}