using Common.Logging;
using Common.Messaging;
using GameInterface.Policies;
using GameInterface.Services.SiegeEvents.Messages;
using HarmonyLib;
using Serilog;
using System;
using System.Collections.Generic;
using System.Reflection;
using TaleWorlds.CampaignSystem.Siege;
namespace GameInterface.Services.SiegeEvents.Patches;


/// <summary>
/// Patches for managing lifetime of <see cref="SiegeEvent"/> objects.
/// </summary>
[HarmonyPatch]
internal class SiegeEventLifetimePatches
{
    static readonly ILogger Logger = LogManager.GetLogger<SiegeEventLifetimePatches>();

    static IEnumerable<MethodBase> TargetMethods() => AccessTools.GetDeclaredConstructors(typeof(SiegeEvent));

    static bool Prefix(ref SiegeEvent __instance)
    {
        // Call original if we call this function
        if (CallOriginalPolicy.IsOriginalAllowed()) return true;

        if (ModInformation.IsClient)
        {
            Logger.Error("Client created unmanaged {name}\n"
                + "Callstack: {callstack}", typeof(SiegeEvent), Environment.StackTrace);
            return true;
        }

        var message = new SiegeEventCreated(__instance);

        MessageBroker.Instance.Publish(__instance, message);

        return true;
    }


    //[HarmonyPatch(typeof(SiegeEvent), "Remove method name here!")]
    //[HarmonyPrefix()]
    private static bool RemovePrefix(ref SiegeEvent __instance)
    {
        // Call original if we call this function
        if (CallOriginalPolicy.IsOriginalAllowed()) return true;

        if (ModInformation.IsClient)
        {
            Logger.Error("Client destroyed unmanaged {name}\n"
            + "Callstack: {callstack}", typeof(SiegeEvent), Environment.StackTrace);
            return false;
        }

        MessageBroker.Instance.Publish(__instance, new SiegeEventDestroyed(__instance));

        return true;
    }
}
