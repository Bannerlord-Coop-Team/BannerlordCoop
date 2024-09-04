using Common.Logging;
using Common.Messaging;
using GameInterface.Policies;
using GameInterface.Services.BesiegerCamps.Messages;
using HarmonyLib;
using Serilog;
using System;
using System.Collections.Generic;
using System.Text;
using TaleWorlds.CampaignSystem.Siege;

namespace GameInterface.Services.BesiegerCamps.Patches;

[HarmonyPatch(typeof(BesiegerCamp))]
internal class BesiegerCampPropertyPatches
{
    static readonly ILogger Logger = LogManager.GetLogger<BesiegerCampPropertyPatches>();

    [HarmonyPatch(nameof(BesiegerCamp.SiegeEvent), MethodType.Setter)]
    static bool Prefix(ref BesiegerCamp __instance, ref SiegeEvent value)
    {
        // Call original if we call this function
        if (CallOriginalPolicy.IsOriginalAllowed()) return true;

        if (ModInformation.IsClient)
        {
            Logger.Error("Client created unmanaged {name}\n"
                + "Callstack: {callstack}", typeof(BesiegerCamp), Environment.StackTrace);
            return true;
        }

        var message = new BesiegerCampSiegeEventChanged(__instance, value);

        MessageBroker.Instance.Publish(__instance, message);

        return true;
    }
}
