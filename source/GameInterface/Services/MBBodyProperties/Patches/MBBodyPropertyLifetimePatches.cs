using Common.Logging;
using Common.Messaging;
using GameInterface.Policies;
using GameInterface.Services.MBBodyProperties.Messages;
using HarmonyLib;
using Serilog;
using System;
using TaleWorlds.Core;
namespace GameInterface.Services.MBBodyProperties.Patches;


/// <summary>
/// Patches for managing lifetime of <see cref="MBBodyProperty"/> objects.
/// </summary>
[HarmonyPatch(typeof(MBBodyProperty))]
internal class MBBodyPropertyLifetimePatches
{
    private static readonly ILogger Logger = LogManager.GetLogger<MBBodyPropertyLifetimePatches>
    ();

    [HarmonyPatch(typeof(MBBodyProperty))]
    [HarmonyPatch(MethodType.Constructor)]
    [HarmonyPrefix]
    private static bool ConstructorPrefix(ref MBBodyProperty __instance)
    {
        // Call original if we call this function
        if (CallOriginalPolicy.IsOriginalAllowed()) return true;

        if (ModInformation.IsClient)
        {
            Logger.Error("Client created unmanaged {name}\n"
            + "Callstack: {callstack}", typeof(MBBodyProperty), Environment.StackTrace);
            return true;
        }

        var message = new MBBodyPropertyCreated(__instance);

        MessageBroker.Instance.Publish(__instance, message);

        return true;
    }
}
