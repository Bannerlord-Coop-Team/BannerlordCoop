using Common.Logging;
using Common.Messaging;
using GameInterface.Policies;
using GameInterface.Services.StanceLinks.Messages;
using HarmonyLib;
using Serilog;
using System;
using System.Collections.Generic;
using System.Reflection;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Siege;
namespace GameInterface.Services.StanceLinks.Patches;


/// <summary>
/// Patches for managing lifetime of <see cref="StanceLink"/> objects.
/// </summary>
[HarmonyPatch]
internal class StanceLinkLifetimePatches
{
    private static readonly ILogger Logger = LogManager.GetLogger<StanceLinkLifetimePatches>();
    private static IEnumerable<MethodBase> TargetMethods() => AccessTools.GetDeclaredConstructors(typeof(StanceLink));

    private static bool Prefix(StanceLink __instance)
    {
        // Call original if we call this function
        if (CallOriginalPolicy.IsOriginalAllowed())
            return true;

        if (ModInformation.IsClient)
        {
            Logger.Error("Client created unmanaged {name}\nCallstack: {callstack}",
                typeof(StanceLink), Environment.StackTrace);
            return true;
        }

        var message = new StanceLinkCreated(__instance);
        MessageBroker.Instance.Publish(__instance, message);

        return true;
    }
}
