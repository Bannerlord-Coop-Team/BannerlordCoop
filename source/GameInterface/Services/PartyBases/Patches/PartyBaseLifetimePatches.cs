using Common.Logging;
using Common.Messaging;
using GameInterface.Policies;
using GameInterface.Services.PartyBases.Messages;
using HarmonyLib;
using Serilog;
using System;
using System.Collections.Generic;
using System.Reflection;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;

namespace GameInterface.Services.PartyBases.Patches;

[HarmonyPatch]
internal class PartyBaseLifetimePatches
{
    static ILogger Logger = LogManager.GetLogger<PartyBase>();

    private static IEnumerable<MethodBase> TargetMethods() => AccessTools.GetDeclaredConstructors(typeof(PartyBase));

    [HarmonyPrefix]
    static bool Prefix(ref PartyBase __instance)
    {
        // Call original if we call this function
        if (CallOriginalPolicy.IsOriginalAllowed()) return true;

        if (ModInformation.IsClient)
        {
            Logger.Error("Client created unmanaged {name}\n"
                + "Callstack: {callstack}", typeof(Equipment), Environment.StackTrace);

            return false;
        }

        var message = new PartyBaseCreated(__instance);
        MessageBroker.Instance.Publish(__instance, message);

        return true;
    }
}