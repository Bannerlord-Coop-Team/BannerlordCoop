using Common.Logging;
using Common.Messaging;
using GameInterface.Policies;
using GameInterface.Services.PartyComponents.Messages;
using HarmonyLib;
using Serilog;
using System;
using System.Collections.Generic;
using System.Reflection;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Party.PartyComponents;
using TaleWorlds.CampaignSystem.Settlements;

namespace GameInterface.Services.PartyComponents.Patches.Lifetime;


/// <summary>
/// Harmony patches for the lifetime of a <see cref="MilitiaPartyComponent"/> object
/// </summary>
[HarmonyPatch]
internal class MilitiaPartyComponentLifetimePatches
{
    private static readonly ILogger Logger = LogManager.GetLogger<MilitiaPartyComponentLifetimePatches>();


    [HarmonyPatch(typeof(MilitiaPartyComponent), MethodType.Constructor, typeof(Settlement))]
    [HarmonyPrefix]
    private static bool Prefix(MilitiaPartyComponent __instance, Settlement settlement)
    {
        // Call original if we call this function
        if (CallOriginalPolicy.IsOriginalAllowed()) return true;

        if (ModInformation.IsClient)
        {
            Logger.Error("Client created unmanaged {name}\n"
                + "Callstack: {callstack}", typeof(MilitiaPartyComponent), Environment.StackTrace);
            return true;
        }

        var message = new PartyComponentCreated(__instance, settlement.StringId);

        MessageBroker.Instance.Publish(__instance, message);

        return true;
    }
}