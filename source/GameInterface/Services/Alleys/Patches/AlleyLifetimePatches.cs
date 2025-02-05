using Common.Logging;
using Common.Messaging;
using GameInterface.Policies;
using HarmonyLib;
using Serilog;
using System;
using TaleWorlds.Core;
using GameInterface.Services.Equipments.Messages.Events;
using GameInterface.Services.Heroes.Messages;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Localization;
using GameInterface.Services.Alleys.Messages;


namespace GameInterface.Services.Alleys.Patches;

/// <summary>
/// Patches for lifecycle of <see cref="Alley"/> objects.
/// </summary>
[HarmonyPatch]
internal class AlleyLifetimePatches
{
    private static readonly ILogger Logger = LogManager.GetLogger<AlleyLifetimePatches>();

    [HarmonyPatch(typeof(Alley), MethodType.Constructor, typeof(Settlement), typeof(string), typeof(TextObject))]
    [HarmonyPrefix]
    private static bool CreateAlleyPrefix(Alley __instance, Settlement settlement, string tag, TextObject name)
    {
        // Call original if we call this function
        if (CallOriginalPolicy.IsOriginalAllowed()) return true;
        
        if (ModInformation.IsClient)
        {   
            Logger.Error("Client created unmanaged {name}\n"
                + "Callstack: {callstack}", typeof(Alley), Environment.StackTrace);
            
            return false;
        }

        MessageBroker.Instance.Publish(__instance, new AlleyCreated(__instance, settlement, tag, name.Value));
            
        return true;
    }
}
