using Common.Logging;
using Common.Messaging;
using GameInterface.Policies;
using GameInterface.Services.PartyComponents.Messages;
using HarmonyLib;
using Serilog;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Linq;
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

    private static IEnumerable<MethodBase> TargetMethods()
    {
        return AccessTools.GetDeclaredConstructors(typeof(MilitiaPartyComponent))
            .Where(m => m.GetParameters().Any(p => p.ParameterType == typeof(Settlement)));
    }

    [HarmonyPrefix]
    private static bool Prefix(MilitiaPartyComponent __instance, Settlement settlement)
    {
        if (CallOriginalPolicy.IsOriginalAllowed()) return true;

        if (ModInformation.IsClient)
        {
            Logger.Error("Client created unmanaged {name}\n" + "Callstack: {callstack}", typeof(MilitiaPartyComponent), Environment.StackTrace);
            return true;
        }

        var message = new PartyComponentCreated(__instance, settlement.StringId);
        MessageBroker.Instance.Publish(__instance, message);

        return true;
    }
}
