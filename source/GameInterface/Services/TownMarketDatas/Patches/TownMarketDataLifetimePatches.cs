using Common;
using Common.Logging;
using Common.Messaging;
using GameInterface.Policies;
using GameInterface.Services.TownMarketDatas.Messages;
using HarmonyLib;
using Serilog;
using System;
using System.Collections.Generic;
using System.Reflection;
using TaleWorlds.CampaignSystem.Settlements;

namespace GameInterface.Services.TownMarketDatas.Patches;

/// <summary>
/// Patches for managing lifetime of <see cref="TownMarketData"/> objects.
/// </summary>
[HarmonyPatch]
internal class TownMarketDataLifetimePatches
{
    static readonly ILogger Logger = LogManager.GetLogger<TownMarketDataLifetimePatches>();

    static IEnumerable<MethodBase> TargetMethods() => AccessTools.GetDeclaredConstructors(typeof(TownMarketData));

    static bool Prefix(ref TownMarketData __instance)
    {
        // Call original if we call this function
        if (CallOriginalPolicy.IsOriginalAllowed()) return true;

        if (ModInformation.IsClient)
        {
            Logger.Error("Client created managed {name}", typeof(TownMarketData));
            return true;
        }

        var message = new TownMarketDataCreated(__instance);

        MessageBroker.Instance.Publish(__instance, message);

        return true;
    }
}