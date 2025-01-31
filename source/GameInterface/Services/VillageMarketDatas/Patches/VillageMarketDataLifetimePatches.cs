using Common.Logging;
using Common.Messaging;
using GameInterface.Policies;
using GameInterface.Services.SiegeEvents.Messages;
using GameInterface.Services.VillageMarketDatas.Messages;
using HarmonyLib;
using Serilog;
using System;
using System.Collections.Generic;
using System.Reflection;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.Siege;

namespace GameInterface.Services.VillageMarketDatas.Patches;

/// <summary>
/// Patches for managing lifetime of <see cref="VillageMarketData"/> objects.
/// </summary>
[HarmonyPatch]
internal class VillageMarketDataLifetimePatches
{
    static readonly ILogger Logger = LogManager.GetLogger<VillageMarketDataLifetimePatches>();

    static IEnumerable<MethodBase> TargetMethods() => AccessTools.GetDeclaredConstructors(typeof(VillageMarketData));

    static bool Prefix(ref VillageMarketData __instance)
    {
        // Call original if we call this function
        if (CallOriginalPolicy.IsOriginalAllowed()) return true;

        if (ModInformation.IsClient)
        {
            Logger.Error("Client created unmanaged {name}\n"
                + "Callstack: {callstack}", typeof(VillageMarketData), Environment.StackTrace);
            return true;
        }

        var message = new VillageMarketDataCreated(__instance);

        MessageBroker.Instance.Publish(__instance, message);

        return true;
    }
}