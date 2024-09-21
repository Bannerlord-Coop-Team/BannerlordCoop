using Common.Logging;
using Common.Messaging;
using GameInterface.Policies;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.PartyBases.Messages;
using HarmonyLib;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;

namespace GameInterface.Services.PartyBases.Patches;

[HarmonyPatch(typeof(PartyBase))]
internal class PartyBaseLifetimePatches
{
    static ILogger Logger = LogManager.GetLogger<PartyBase>();

    static IEnumerable<MethodBase> TargetMethods() => AccessTools.GetDeclaredConstructors(typeof(PartyBase));

    static bool Prefix(PartyBase __instance)
    {
        // Call original if we call this function
        if (CallOriginalPolicy.IsOriginalAllowed()) return true;

        if (ModInformation.IsClient)
        {
            Logger.Error("Client created unmanaged {name}\n"
                + "Callstack: {callstack}", typeof(Equipment), Environment.StackTrace);


            return true;
        }

        var message = new PartyBaseCreated(__instance);
        MessageBroker.Instance.Publish(__instance, message);

        return true;
    }
}
