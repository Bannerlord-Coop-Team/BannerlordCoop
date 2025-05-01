using Common.Logging;
using Common.Messaging;
using GameInterface.Policies;
using GameInterface.Services.TroopRosters.Handlers;
using HarmonyLib;
using Serilog;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.Siege;

namespace GameInterface.Services.TroopRosters.Patches;

[HarmonyPatch(typeof(TroopRoster))]
internal class TroopRosterLifetimePatches
{
    private static ILogger Logger = LogManager.GetLogger<TroopRosterLifetimePatches>();
    static IEnumerable<MethodBase> TargetMethods() => new MethodBase[] {
        AccessTools.Constructor(typeof(TroopRoster))
    };

    [HarmonyPrefix]
    static void CtorPrefix(TroopRoster __instance)
    {
        // Call original if we call this function
        if (CallPolicy.IsOriginalAllowed()) return;

        if (CallPolicy.SkipIfClient(Logger, out var returnResult)) return;

        ContainerProvider.TryResolve<IMessageBroker>(out var messageBroker);
        messageBroker?.Publish(__instance, new TroopRosterCreated(__instance));
    }
}
